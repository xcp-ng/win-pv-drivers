using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Web;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Ole;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace XenPlus;

sealed class MainWindow() : Window(typeof(MainWindow).FullName!, "xenplus_session main window") {
    const int MaxClipboardSize = 100000;
    const int NotifyIconId = 1;
    const uint TrayMenuMessage = PInvoke.WM_APP;

    readonly Lazy<AppConfig> _config = new();

    readonly ClipboardPipe _pipe = new();
    readonly CancellationTokenSource _cts = new();
    readonly SemaphoreSlim _clipboardUpdateLock = new(1, 1);

    bool _listened = false;
    Task? _receiver = null;

    /// <summary>
    /// Was the last clipboard sequence number due to our own setting?
    /// </summary>
    uint _lastSeq = 0;

    bool _hasTrayIcon = false;
    readonly Lazy<DestroyMenuSafeHandle> _trayMenu = new(() => Resources.LoadMenu(Resources.TrayMenu));

    bool _showingAbout = false;

    bool _closing = false;

    HMENU GetTrayMenu() {
        using var menuScope = _trayMenu.Value.Borrow();
        var result = PInvoke.GetSubMenu((HMENU)menuScope.DangerousHandle, 0);
        if (result == HMENU.Null) {
            throw new Win32Exception(nameof(PInvoke.GetSubMenu));
        }
        return result;
    }

    static ushort LOWORD(WPARAM value) => (ushort)(value & 0xffff);
    static ushort HIWORD(WPARAM value) => (ushort)((value >> 16) & 0xffff);

    static ushort LOWORD(LPARAM value) => (ushort)(value & 0xffff);
    static ushort HIWORD(LPARAM value) => (ushort)((value >> 16) & 0xffff);

    async Task ReceiveClipboardAsync(HWND hwnd) {
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

    async void OnClipboardUpdateAsync(HWND hwnd) {
        try {
            // both the clipboard read and pipe write must not overlap
            using var updateScope = await _clipboardUpdateLock.EnterScopeAsync(_cts.Token);

            if (!_listened) {
                return;
            }
            var seq = PInvoke.GetClipboardSequenceNumber();
            if (seq == _lastSeq) {
                return;
            }
            _lastSeq = seq;

            bool opened = false;
            string s;
            try {
                if (!PInvoke.OpenClipboard(hwnd)) {
                    throw new Win32Exception(nameof(PInvoke.OpenClipboard));
                }
                opened = true;

                using var cb = ClipboardSafeHandle.GetClipboard(CLIPBOARD_FORMAT.CF_UNICODETEXT);
                s = cb.GetString();
            } finally {
                if (opened) {
                    PInvoke.CloseClipboard();
                    opened = false;
                }
            }

            await _pipe.SendAsync(s, _cts.Token);
        } catch (Exception ex) {
            Trace.TraceInformation("writing client message failed: {0}", ex.ToString());
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
        _receiver = ReceiveClipboardAsync(hwnd);

        if (_config.Value.ShowTrayIcon) {
            try {
                CreateTrayIcon(hwnd);
            } catch (Exception ex) {
                Trace.TraceInformation("cannot create tray icon: {0}", ex.ToString());
            }
        }
        return (LRESULT)0;
    }

    async void OnCloseAsync(HWND hwnd) {
        if (Interlocked.Exchange(ref _closing, true)) {
            return;
        }
        _cts.Cancel();
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
        if (Interlocked.Exchange(ref _receiver, null) is Task receiver) {
            try {
                await receiver;
            } catch {
            }
        }
        PInvoke.DestroyWindow(hwnd);
    }

    LRESULT OnContextMenu(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        try {
            var x = unchecked((int)(short)LOWORD(wparam));
            var y = unchecked((int)(short)HIWORD(wparam));

            PInvoke.SetForegroundWindow(hwnd);

            TRACK_POPUP_MENU_FLAGS flags = 0;
            if (PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_MENUDROPALIGNMENT) != 0) {
                flags |= TRACK_POPUP_MENU_FLAGS.TPM_RIGHTALIGN;
            } else {
                flags |= TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN;
            }

            unsafe {
                if (!PInvoke.TrackPopupMenuEx(
                    GetTrayMenu(),
                    (uint)flags,
                    x,
                    y,
                    hwnd,
                    null)) {
                    throw new Win32Exception(nameof(PInvoke.TrackPopupMenuEx));
                }
            }

            return (LRESULT)0;
        } catch (Exception ex) {
            Trace.TraceInformation("cannot show tray menu: {0}", ex.ToString());
            return (LRESULT)0;
        }
    }

    LRESULT OnAbout(HWND hwnd) {
        if (_showingAbout) {
            return (LRESULT)0;
        }
        _showingAbout = true;
        try {
            using var td = new TaskDialog();
            using var hinst = PInvoke.GetModuleHandle(null);
            using var hinstScope = hinst.Borrow();

            td.Instance = (HINSTANCE)hinstScope.DangerousHandle;
            td.EnableHyperlinks = true;
            td.AllowDialogCancellation = true;
            td.PositionRelativeToWindow = true;
            td.CloseButton = true;

            td.WindowTitle = $"About {VersionInfo.ProductName}";

            // TaskDialog in hyperlink mode uses a weird text flavor that's not simply HTML. But HTML encode anyway,
            // just to be sure.
            td.MainInstruction = HttpUtility.HtmlEncode(VersionInfo.VendorName) +
                " " +
                HttpUtility.HtmlEncode(VersionInfo.Description);

            td.Content = $"""
            Version: {VersionInfo.FileVersion}
            Package version: {HttpUtility.HtmlEncode(VersionInfo.ProductName)} {VersionInfo.ProductVersion}
            {HttpUtility.HtmlEncode(VersionInfo.Copyright)}
            """;

            unsafe {
                td.MainIconResource = Resources.MAKEINTRESOURCE(Resources.Icon);
            }

            if (!string.IsNullOrEmpty(VersionInfo.ProductUrl)) {
                td.Footer = $@"<a href=""{HttpUtility.HtmlAttributeEncode(VersionInfo.ProductUrl)}"">" +
                    HttpUtility.HtmlEncode(VersionInfo.ProductUrl) +
                    "</a>";
            }

            td.HyperlinkClicked += (sender, args) => {
                try {
                    if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)) {
                        var psi = new ProcessStartInfo() {
                            FileName = uri.ToString(),
                            UseShellExecute = true,
                        };
                        Process.Start(psi)?.Dispose();
                    }
                } catch {
                }
            };

            td.Show(false, _cts.Token);
            return (LRESULT)0;
        } finally {
            _showingAbout = false;
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
            case Resources.TrayMenu_About:
                return OnAbout(hwnd);
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
                OnClipboardUpdateAsync(hwnd);
                return (LRESULT)0;
            case TrayMenuMessage:
                return (uint)LOWORD(lparam) switch {
                    PInvoke.WM_CONTEXTMENU => OnContextMenu(hwnd, msg, wparam, lparam),
                    _ => (LRESULT)0,
                };
            case PInvoke.WM_COMMAND:
                return OnCommand(hwnd, msg, wparam, lparam);
            case PInvoke.WM_WINDOWPOSCHANGING:
                unsafe {
                    if (lparam != 0) {
                        ((WINDOWPOS*)lparam.Value)->flags &= ~SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
                    }
                }
                return base.WndProc(hwnd, msg, wparam, lparam);
            case PInvoke.WM_QUERYENDSESSION:
                PInvoke.SendMessage(hwnd, PInvoke.WM_CLOSE, 0, 0);
                return (LRESULT)1;
            case PInvoke.WM_CLOSE:
                OnCloseAsync(hwnd);
                return (LRESULT)0;
            case PInvoke.WM_DESTROY:
                PInvoke.PostQuitMessage(0);
                return (LRESULT)0;
            default:
                return base.WndProc(hwnd, msg, wparam, lparam);
        }
    }
}
