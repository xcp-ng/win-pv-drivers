using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Windows.Win32;
using XenPlus.Clipboard;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class ClipboardOptions {
    public bool Enabled { get; set; } = true;
    public bool UnsafeAllowAnySessionForTest { get; set; } = false;
}

sealed class ClipboardClient(NamedPipeServerStream _stream, CancellationToken _ct = default) : IDisposable {
    public NamedPipeServerStream Stream => _stream;
    public SemaphoreSlim WriteLock { get; } = new(1, 1);
    public CancellationTokenSource CancellationTokenSource { get; }
        = CancellationTokenSource.CreateLinkedTokenSource(_ct);

    public void Dispose() {
        Stream.Dispose();
        WriteLock.Dispose();
        CancellationTokenSource.Dispose();
    }
}

sealed class ClipboardFeature(
    IHostLifetime _hostLifetime,
    IOptions<ClipboardOptions> _options,
    XenIfaceSource _xi,
    ILogger<ClipboardFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    const string SetClipboardPath = "data/set_clipboard";
    const string ReportClipboardPath = "data/report_clipboard";
    const string ClipboardPipePath = @"ProtectedPrefix\Administrators\XenplusClipboardFeature";
    /// <remarks>
    /// <para>NT AUTHORITY\INTERACTIVE Allow WriteData, Read, Synchronize</para>
    /// <para>NT AUTHORITY\SYSTEM Allow FILE_GENERIC_ALL</para>
    /// <para>BUILTIN\Administrators Allow FILE_GENERIC_ALL</para>
    /// </remarks>
    const string ClipboardPipeSddl = @"D:(A;;0x12008b;;;IU)(A;;FA;;;SY)(A;;FA;;;BA)";
    const uint InvalidSessionId = 0xffffffff;
    const int MaxClipboardChunks = 100;
    const int ClientChunkSize = 1024;
    const int MaxIncomingMessageSize = ClientChunkSize * MaxClipboardChunks;

    static readonly Lazy<PipeSecurity> _secure = new(static () => {
        var secure = new PipeSecurity();
        secure.SetSecurityDescriptorSddlForm(ClipboardPipeSddl);
        return secure;
    });

    /// <summary>
    /// from the host
    /// </summary>
    XenIfaceWatch? _setClipboard = null;

    /// <summary>
    /// from the guest
    /// </summary>
    XenIfaceWatch? _reportClipboard = null;

    /// <summary>
    /// For active clients
    /// </summary>
    readonly ReferenceCount _active = new();

    /// <summary>
    /// for the current reporter
    /// </summary>
    readonly SemaphoreSlim _reportClipboardLock = new(1, 1);

    readonly SemaphoreSlim _lock = new(1, 1);
    /// <summary>
    /// locked
    /// </summary>
    readonly Dictionary<uint, ClipboardClient> _clients = [];
    /// <summary>
    /// locked
    /// </summary>
    readonly Queue<string> _setClipboardChunks = [];

    async Task ServeClientLoop(uint sid, ClipboardClient client, CancellationToken ct = default) {
        try {
            while (!ct.IsCancellationRequested) {
                var lengthBytes = new byte[sizeof(int)];
                await client.Stream.ReadExactlyAsync(lengthBytes, ct);
                var length = BitConverter.ToInt32(lengthBytes);
                if (length <= 0 || length > MaxIncomingMessageSize) {
                    throw new InvalidDataException("client went over the line");
                }

                var limiter = new StreamReadLimiter(client.Stream, length);
                var msg = await JsonSerializer.DeserializeAsync(
                    limiter,
                    ClipboardMessageContext.Default.ClientMessage,
                    ct);

                if (msg is ReportClipboardMessage reportClipboard) {
                    using var reportScope = await _reportClipboardLock.EnterScopeAsync(ct);

                    var consoleSid = PInvoke.WTSGetActiveConsoleSessionId();
                    if (consoleSid == InvalidSessionId || sid != consoleSid) {
                        continue;
                    }

                    _logger.LogDebug("ReportClipboard length {}", reportClipboard?.Text?.Length ?? 0);

                    foreach (var chunk in (reportClipboard?.Text ?? "").Chunk(ClientChunkSize).Append([])) {
                        var wait = _reportClipboard!.WaitOneAsync(5000, 1, ct);
                        using (var h = _xi.Lock()) {
                            h.StoreWrite(ReportClipboardPath, new(chunk), false);
                        }
                        await wait;
                    }
                }
            }
        } catch (Exception ex) {
            _logger.LogDebug(ex, "ServeClientLoop failed");
        }
    }

    async Task ServeClient(NamedPipeServerStream pipe, CancellationToken ct = default) {
        using var lifetime = _active.EnterScope();
        using var client = new ClipboardClient(pipe, ct);
        uint sid;

        try {
            if (!PInvoke.GetNamedPipeClientSessionId(client.Stream.SafePipeHandle, out sid)) {
                throw new Win32Exception();
            }
            // to prevent flipflopping between different user clipboard agents, if the session is already connected,
            // drop the new connection instead
            using var scope = await _lock.EnterScopeAsync(ct);
            if (!_clients.TryAdd(sid, client)) {
                throw new Exception($"Client SID {sid} already exists");
            }
        } catch (Exception ex) {
            _logger.LogDebug(ex, "Refused new client");
            return;
        }

        try {
            await ServeClientLoop(sid, client, client.CancellationTokenSource.Token);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to serve client {}", sid);
        } finally {
            // the main loop doesn't need to be inside the main lock, only the final destruction does, because
            // ServeClient holds ownership of the client object
            client.CancellationTokenSource.Cancel();
            using var scope = await _lock.EnterScopeAsync(CancellationToken.None);
            if (_clients.TryGetValue(sid, out var existing) && ReferenceEquals(existing, client)) {
                _clients.Remove(sid);
            }
            // drain OnSetClipboard too
            using var clientScope = await client.WriteLock.EnterScopeAsync(CancellationToken.None);
        }
    }

    async Task<(ClipboardClient, SetClipboardMessage, SemaphoreScope)?> GetClientOnSetClipboard(CancellationToken ct) {
        // ensure that the collection runs serialized
        using var scope = await _lock.EnterScopeAsync(ct);

        using (var h = _xi.Lock()) {
            string chunk;
            try {
                chunk = h.StoreRead(SetClipboardPath);
            } catch {
                // watches triggered by store removes
                return null;
            }
            try {
                h.StoreRemove(SetClipboardPath);
            } catch {
            }
            if (chunk.Length != 0) {
                if (_setClipboardChunks.Count < MaxClipboardChunks) {
                    _setClipboardChunks.Enqueue(chunk);
                }
                return null;
            }
        }

        // from this point on, we got the entire content from the host and are going to give it to the active client

        var msg = new SetClipboardMessage() {
            Text = string.Join("", _setClipboardChunks)
        };
        // regardless of whether we have a listener or not, we want to consume the set_clipboard chunk train anyway
        _setClipboardChunks.Clear();

        var allowTest = _options.Value.UnsafeAllowAnySessionForTest;
        var sid = PInvoke.WTSGetActiveConsoleSessionId();
        if (sid == InvalidSessionId && !allowTest) {
            return null;
        }
        if (!_clients.TryGetValue(sid, out var client)) {
            if (!allowTest) {
                return null;
            }
            client = _clients.Values.FirstOrDefault();
            if (client == null) {
                return null;
            }
        }

        // we want to avoid holding the global lock when using the client pipe
        // at the same time, we want to make sure that OnSetClipboard cannot use a dying client after ServeClient has
        // begun to tear it down
        // so we make the client lock partially overlap the global lock while respecting the lock order
        return (client, msg, await client.WriteLock.EnterScopeAsync(client.CancellationTokenSource.Token));
    }

    /// <summary>
    /// attention: runs in off-thread
    /// </summary>
    async Task OnSetClipboard(object? sender, XenIfaceWatchEventArgs args, CancellationToken ct = default) {
        var found = await GetClientOnSetClipboard(ct);
        if (found == null) {
            return;
        }
        var (client, msg, clientScope) = found.Value;

        try {
            using (clientScope) {
                var json = JsonSerializer.SerializeToUtf8Bytes(
                    msg,
                    ClipboardMessageContext.Default.ServerMessage);
                var lengthBytes = BitConverter.GetBytes(json.Length);
                await client.Stream.WriteAsync(lengthBytes, ct);
                await client.Stream.WriteAsync(json, ct);
            }
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            _logger.LogDebug(ex, "Client threw exception");
            // let the main task handle removing the client
            client.CancellationTokenSource.Cancel();
        }
    }

    protected override async Task ExecuteFeatureAsync(CancellationToken stoppingToken) {
        if (!_options.Value.Enabled) {
            return;
        }
        if (_options.Value.UnsafeAllowAnySessionForTest) {
            _logger.LogWarning("""
            ClipboardOptions.UnsafeAllowAnySessionForTest mode is enabled, which is insecure.
            Anyone can access the clipboard.
            """);
        }

        try {
            using var h = _xi.Lock();
            h.StoreRemove(SetClipboardPath);
            h.StoreRemove(ReportClipboardPath);
        } catch (XenIfaceNotFoundException) {
        }

        async void onSetClipboard(object? sender, XenIfaceWatchEventArgs args) {
            try {
                await OnSetClipboard(sender, args, stoppingToken);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                // GetClientOnSetClipboard exploding can't be good news
                Environment.FailFast(nameof(OnSetClipboard), ex);
            }
        }

        bool watched = false;
        try {
            Interlocked.Exchange(ref _setClipboard, _xi.WatchAdd(SetClipboardPath));
            Interlocked.Exchange(ref _reportClipboard, _xi.WatchAdd(ReportClipboardPath));
            _setClipboard.WatchTriggered += onSetClipboard;
            watched = true;
            while (!stoppingToken.IsCancellationRequested) {
                var pipe = SecureNamedPipes.Listen(
                    ClipboardPipePath,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    HandleInheritability.None,
                    true,
                    _secure.Value);
                try {
                    await pipe.WaitForConnectionAsync(stoppingToken);
                } catch {
                    pipe.Dispose();
                    throw;
                }
                _ = ServeClient(pipe, stoppingToken);
            }
        } finally {
            if (watched) {
                _setClipboard!.WatchTriggered -= onSetClipboard;
            }
            await _active.WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);
            Interlocked.Exchange(ref _reportClipboard, null)?.Dispose();
            Interlocked.Exchange(ref _setClipboard, null)?.Dispose();
        }
    }
}
