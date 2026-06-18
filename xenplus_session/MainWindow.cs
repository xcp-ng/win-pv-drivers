using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Ole;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace XenPlus;

sealed class MainWindow() : Window(nameof(MainWindow), "xenplus_session main window") {
    const int MaxClipboardSize = 100000;
    const int NotifyIconId = 1;
    const uint TrayMenuMessage = PInvoke.WM_APP;

    readonly Lazy<AppConfig> _config = new();

    bool _listened = false;
    readonly ClipboardPipe _pipe = new();
    readonly CancellationTokenSource _cts = new();
    Task? _receiver = null;
    /// <summary>
    /// Was the last clipboard sequence number due to our own setting?
    /// </summary>
    uint _lastSeq = 0;
    bool _hasTrayIcon = false;
    readonly Lazy<DestroyMenuSafeHandle> _trayMenu = new(() => Resources.LoadMenu(Resources.TrayMenu));

    HMENU GetTrayMenu() {
        using var menuScope = _trayMenu.Value.Borrow();
        var result = PInvoke.GetSubMenu((HMENU)menuScope.DangerousHandle, 0);
        if (result == HMENU.Null) {
            throw new Win32Exception(nameof(PInvoke.GetSubMenu));
        }
        return result;
    }

    static ushort LOWORD(WPARAM value) => (ushort)(value.Value & 0xffff);
    static ushort HIWORD(WPARAM value) => (ushort)((value >> 16) & 0xffff);

    static short LOWORD(LPARAM value) => (short)(value.Value & 0xffff);
    static short HIWORD(LPARAM value) => (short)((value >> 16) & 0xffff);

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

    void CreateTrayIcon(HWND hwnd) {
        using var hicon = Resources.LoadIcon(Resources.Icon, false);
        using var hiconScope = hicon.Borrow();
        var notifyData = new NOTIFYICONDATAW() {
            cbSize = (uint)Unsafe.SizeOf<NOTIFYICONDATAW>(),
            hWnd = hwnd,
            uID = NotifyIconId,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE |
                NOTIFY_ICON_DATA_FLAGS.NIF_ICON |
                NOTIFY_ICON_DATA_FLAGS.NIF_TIP |
                NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP,
            uCallbackMessage = TrayMenuMessage,
            hIcon = (HICON)hiconScope.DangerousHandle,
            szTip = VersionInfo.Description,
            uVersion = PInvoke.NOTIFYICON_VERSION_4,
        };
        if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, notifyData)) {
            throw new Exception("cannot create tray icon");
        }
        _hasTrayIcon = true;
        try {
            if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, notifyData)) {
                throw new Exception("cannot set tray icon version");
            }
        } catch {
            DestroyTrayIcon(hwnd);
            throw;
        }
    }

    void DestroyTrayIcon(HWND hwnd) {
        if (!_hasTrayIcon) {
            return;
        }
        var notifyData = new NOTIFYICONDATAW() {
            cbSize = (uint)Unsafe.SizeOf<NOTIFYICONDATAW>(),
            hWnd = hwnd,
            uID = NotifyIconId,
        };
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, notifyData);
        _hasTrayIcon = false;
    }

    LRESULT OnCreate(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        if (!PInvoke.AddClipboardFormatListener(hwnd)) {
            throw new Win32Exception(nameof(PInvoke.AddClipboardFormatListener));
        }
        _listened = true;
        _receiver = ReceiveClipboard(hwnd);

        if (_config.Value.ShowTrayIcon) {
            try {
                CreateTrayIcon(hwnd);
            } catch (Exception ex) {
                Trace.TraceInformation("cannot create tray icon: {0}", ex.ToString());
            }
        }
        return (LRESULT)0;
    }

    LRESULT OnDestroy(HWND hwnd) {
        if (_trayMenu.IsValueCreated) {
            _trayMenu.Value.Dispose();
        }
        if (_hasTrayIcon) {
            DestroyTrayIcon(hwnd);
        }
        if (_listened) {
            PInvoke.RemoveClipboardFormatListener(hwnd);
            _listened = false;
        }
        PInvoke.PostQuitMessage(0);
        return (LRESULT)0;
    }

    LRESULT OnContextMenu(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        try {
            if (!PInvoke.GetCursorPos(out var pt)) {
                throw new Win32Exception(nameof(PInvoke.GetCursorPos));
            }
            PInvoke.SetForegroundWindow(hwnd);

            var align = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_MENUDROPALIGNMENT);
            var flags = TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN;
            if (align != 0) {
                flags |= TRACK_POPUP_MENU_FLAGS.TPM_RIGHTALIGN;
            } else {
                flags |= TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN;
            }
            unsafe {
                if (!PInvoke.TrackPopupMenuEx(
                    GetTrayMenu(),
                    (uint)flags,
                    pt.X,
                    pt.Y,
                    hwnd,
                    null)) {
                    throw new Win32Exception(nameof(PInvoke.TrackPopupMenuEx));
                }
            }
            // cargo culted
            PInvoke.PostMessage(hwnd, PInvoke.WM_NULL, 0, 0);

            return (LRESULT)0;
        } catch (Exception ex) {
            Trace.TraceInformation("cannot show tray menu: {0}", ex.ToString());
            return (LRESULT)0;
        }
    }

    LRESULT OnCommand(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        if (HIWORD(wparam) != 0) {
            return base.WndProc(hwnd, msg, wparam, lparam);
        }
        switch (LOWORD(wparam)) {
            case Resources.TrayMenu_Hide:
                try {
                    _config.Value.ShowTrayIcon = false;
                } catch (Exception ex) {
                    Trace.TraceInformation("cannot set config: {0}", ex.ToString());
                }
                DestroyTrayIcon(hwnd);
                return (LRESULT)0;
            case Resources.TrayMenu_Exit:
                PInvoke.SendMessage(hwnd, PInvoke.WM_CLOSE, 0, 0);
                return (LRESULT)0;
            default:
                return (LRESULT)0;
        }
    }

    protected override LRESULT WndProc(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        switch (msg) {
            case PInvoke.WM_CREATE:
                return OnCreate(hwnd, msg, wparam, lparam);
            case PInvoke.WM_CLIPBOARDUPDATE:
                OnClipboardUpdate(hwnd);
                return (LRESULT)0;
            case TrayMenuMessage:
                return (uint)LOWORD(lparam) switch {
                    PInvoke.WM_CONTEXTMENU => OnContextMenu(hwnd, msg, wparam, lparam),
                    _ => (LRESULT)0,
                };
            case PInvoke.WM_COMMAND:
                return OnCommand(hwnd, msg, wparam, lparam);
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
}
