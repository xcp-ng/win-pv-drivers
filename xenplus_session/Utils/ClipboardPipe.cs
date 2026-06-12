using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text.Json;
using XenPlus.Clipboard;

namespace XenPlus;

sealed class ClipboardPipe : IDisposable {
    const string ClipboardPipePath = @"ProtectedPrefix\Administrators\XenplusClipboardFeature";
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
                    var msg = await JsonSerializer.DeserializeAsync(
                    _pipe,
                    ClipboardMessageContext.Default.ServerMessage,
                    ct);
                    if (msg is SetClipboardMessage setClipboard) {
                        text = setClipboard.Text;
                    }
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
        await EnsureOpened(ct);
        if (_pipe == null) {
            throw new IOException("cannot connect to pipe");
        }
        var msg = new ReportClipboardMessage() { Text = data };
        await JsonSerializer.SerializeAsync(_pipe, msg, ClipboardMessageContext.Default.ClientMessage, ct);
    }

    public void Dispose() {
        _pipe?.Dispose();
        _pipe = null;
    }
}
