using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text.Json;
using XenPlus.Clipboard;

namespace XenPlus;

sealed class ClipboardPipe : IDisposable {
    const string ClipboardPipePath = @"ProtectedPrefix\Administrators\XenplusClipboardFeature";
    const int MaxIncomingMessageSize = 1024 * 1024;
    static readonly TimeSpan PipeRetryDelayMilliseconds = TimeSpan.FromMilliseconds(10000);

    NamedPipeClientStream? _pipe = null;

    async Task EnsureOpened(CancellationToken ct = default) {
        if (_pipe != null) {
            return;
        }
        try {
            _pipe = new(
                ".",
                ClipboardPipePath,
                PipeAccessRights.Read | PipeAccessRights.WriteData,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification,
                HandleInheritability.None);
            await _pipe.ConnectAsync(100, ct);
        } catch (TimeoutException) {
            Dispose();
        } catch (Exception ex) {
            Trace.TraceInformation("cannot open pipe: {0}", ex.ToString());
            Dispose();
        }
    }

    public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken ct = default) {
        while (!ct.IsCancellationRequested) {
            await EnsureOpened(ct);
            if (_pipe == null) {
                await Task.Delay(PipeRetryDelayMilliseconds, ct);
            } else {
                string? text = null;
                try {
                    var lengthBytes = new byte[sizeof(int)];
                    await _pipe.ReadExactlyAsync(lengthBytes, ct);
                    var length = BitConverter.ToInt32(lengthBytes);
                    if (length <= 0 || length > MaxIncomingMessageSize) {
                        throw new InvalidDataException("server sent invalid length");
                    }

                    ServerMessage? msg;
                    var frame = ArrayPool<byte>.Shared.Rent(length);
                    try {
                        await _pipe.ReadExactlyAsync(frame.AsMemory(0, length), ct);
                        msg = JsonSerializer.Deserialize(
                            frame.AsSpan(0, length),
                            ClipboardMessageContext.Default.ServerMessage);
                    } finally {
                        ArrayPool<byte>.Shared.Return(frame, clearArray: true);
                    }
                    if (msg is SetClipboardMessage setClipboard) {
                        text = setClipboard.Text;
                    }
                } catch (OperationCanceledException) {
                    Dispose();
                } catch (Exception ex) {
                    Trace.TraceInformation("reading server message failed: {0}", ex.ToString());
                    Dispose();
                }
                if (text != null) {
                    yield return text;
                }
            }
        }
    }

    public async Task SendAsync(string data, CancellationToken ct = default) {
        try {
            await EnsureOpened(ct);
            if (_pipe == null) {
                throw new IOException("cannot connect to pipe");
            }
            var msg = new ReportClipboardMessage() { Text = data };
            var json = JsonSerializer.SerializeToUtf8Bytes(msg, ClipboardMessageContext.Default.ClientMessage);
            var lengthBytes = BitConverter.GetBytes(json.Length);
            await _pipe.WriteAsync(lengthBytes, ct);
            await _pipe.WriteAsync(json, ct);
        } catch (Exception ex) {
            Trace.TraceInformation("pipe communication error: {0}", ex.ToString());
            Dispose();
        }
    }

    /// <summary>
    /// <see cref="ClipboardPipe"/> is still reusable even after disposing.
    /// </summary>
    public void Dispose() {
        _pipe?.Dispose();
        _pipe = null;
    }
}
