using System.ComponentModel;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Ole;

namespace XenPlus;

sealed class MainWindow() : Window(nameof(MainWindow), "xenplus_session main window") {
    const int MaxClipboardSize = 100000;

    bool _listened = false;
    readonly ClipboardPipe _pipe = new();
    readonly CancellationTokenSource _cts = new();
    Task? _receiver = null;
    uint _lastSeq = 0;

    async Task ReceiveClipboard(HWND hwnd) {
        await foreach (var data in _pipe.ReceiveAsync(_cts.Token)) {
            bool opened = false;
            try {
                if (!PInvoke.OpenClipboard(hwnd)) {
                    throw new Win32Exception(nameof(PInvoke.OpenClipboard));
                }
                opened = true;

                var span = data.AsSpan();
                if (span.Length > MaxClipboardSize) {
                    span = span[..MaxClipboardSize];
                }

                using var cb = ClipboardSafeHandle.CreateString(span);
                cb.SetClipboard();
                _lastSeq = PInvoke.GetClipboardSequenceNumber();
            } catch (Exception ex) {
                Trace.TraceInformation("setting clipboard failed: {0}", ex.ToString());
            } finally {
                if (opened) {
                    PInvoke.CloseClipboard();
                }
            }
        }
    }

    async void OnClipboardUpdate(HWND hwnd) {
        var seq = PInvoke.GetClipboardSequenceNumber();
        if (seq == _lastSeq) {
            return;
        }
        _lastSeq = seq;

        bool opened = false;
        try {
            if (!PInvoke.OpenClipboard(hwnd)) {
                throw new Win32Exception(nameof(PInvoke.OpenClipboard));
            }
            opened = true;

            string s;
            using (var cb = ClipboardSafeHandle.GetClipboard(CLIPBOARD_FORMAT.CF_UNICODETEXT)) {
                s = cb.GetString();
            }

            await _pipe.SendAsync(s, _cts.Token);
        } catch (Exception ex) {
            Trace.TraceInformation("writing client message failed: {0}", ex.ToString());
        } finally {
            if (opened) {
                PInvoke.CloseClipboard();
            }
        }
    }

    protected override LRESULT WndProc(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        switch (msg) {
            case PInvoke.WM_CREATE:
                return OnCreate(hwnd, msg, wparam, lparam);
            case PInvoke.WM_CLIPBOARDUPDATE:
                OnClipboardUpdate(hwnd);
                return (LRESULT)0;
            case PInvoke.WM_QUERYENDSESSION:
                PInvoke.SendMessage(hwnd, PInvoke.WM_CLOSE, 0, 0);
                return (LRESULT)1;
            case PInvoke.WM_DESTROY:
                _cts.Cancel();
                try {
                    _receiver?.GetAwaiter().GetResult();
                } catch {
                }
                return OnDestroy(hwnd);
            default:
                return base.WndProc(hwnd, msg, wparam, lparam);
        }
    }

    LRESULT OnCreate(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        if (!PInvoke.AddClipboardFormatListener(hwnd)) {
            throw new Win32Exception(nameof(PInvoke.AddClipboardFormatListener));
        }
        _listened = true;
        _receiver = ReceiveClipboard(hwnd);
        return (LRESULT)0;
    }

    LRESULT OnDestroy(HWND hwnd) {
        PInvoke.PostQuitMessage(0);
        if (_listened) {
            PInvoke.RemoveClipboardFormatListener(hwnd);
        }
        return (LRESULT)0;
    }
}
