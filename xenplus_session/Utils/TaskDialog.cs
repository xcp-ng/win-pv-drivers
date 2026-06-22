using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;

namespace XenPlus;

record struct TaskDialogResult(int Button, int RadioButton, bool VerificationFlagChecked) {
}

sealed class TaskDialogHyperlinkClickedEventArgs : EventArgs {
    public string? Uri { get; init; }
}

class TaskDialog : IDisposable {
    readonly GCHandle _gch;
    bool _disposed = false;
    bool _busy = false;
    volatile bool _cancelling = false;

    TASKDIALOGCONFIG _config;

    public TaskDialog() {
        _gch = GCHandle.Alloc(this);

        try {
            _config = new() {
                cbSize = (uint)Unsafe.SizeOf<TASKDIALOGCONFIG>(),
                dwFlags = TASKDIALOG_FLAGS.TDF_USE_HICON_MAIN |
                TASKDIALOG_FLAGS.TDF_USE_HICON_FOOTER |
                TASKDIALOG_FLAGS.TDF_CALLBACK_TIMER,
                pfCallback = &TaskDialogCallback,
                lpCallbackData = GCHandle.ToIntPtr(_gch),
            };
        } catch {
            Dispose();
            throw;
        }
    }

    public bool IsBusy => _busy;

    public delegate void TaskDialogHyperlinkClickedEventHandler(
        object? sender,
        TaskDialogHyperlinkClickedEventArgs args);
    public event TaskDialogHyperlinkClickedEventHandler? HyperlinkClicked;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static HRESULT TaskDialogCallback(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam, nint refData) {
        var gch = GCHandle.FromIntPtr(refData);
        var self = gch.Target as TaskDialog ?? throw new NullReferenceException("cannot resolve TaskDialog");

        switch ((TASKDIALOG_NOTIFICATIONS)msg) {
            case TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED:
                var param = Marshal.PtrToStringUni(lparam.Value);
                self.HyperlinkClicked?.Invoke(self, new() { Uri = param });
                break;
            case TASKDIALOG_NOTIFICATIONS.TDN_TIMER:
                if (self._cancelling) {
                    PInvoke.SendMessage(hwnd, PInvoke.WM_CLOSE, 0, 0);
                }
                break;
            default:
                break;
        }

        return HRESULT.S_OK;
    }

    public HWND Parent {
        get => _config.hwndParent;
        set => _config.hwndParent = value;
    }

    public HINSTANCE Instance {
        get => _config.hInstance;
        set => _config.hInstance = value;
    }

    public bool EnableHyperlinks {
        get {
            return (_config.dwFlags & TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS) != 0;
        }
        set {
            if (value) {
                _config.dwFlags |= TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS;
            } else {
                _config.dwFlags &= ~TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS;
            }
        }
    }

    public bool AllowDialogCancellation {
        get {
            return (_config.dwFlags & TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION) != 0;
        }
        set {
            if (value) {
                _config.dwFlags |= TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
            } else {
                _config.dwFlags &= ~TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
            }
        }
    }

    public bool PositionRelativeToWindow {
        get {
            return (_config.dwFlags & TASKDIALOG_FLAGS.TDF_POSITION_RELATIVE_TO_WINDOW) != 0;
        }
        set {
            if (value) {
                _config.dwFlags |= TASKDIALOG_FLAGS.TDF_POSITION_RELATIVE_TO_WINDOW;
            } else {
                _config.dwFlags &= ~TASKDIALOG_FLAGS.TDF_POSITION_RELATIVE_TO_WINDOW;
            }
        }
    }

    public bool OkButton {
        get {
            return (_config.dwCommonButtons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON) != 0;
        }
        set {
            if (value) {
                _config.dwCommonButtons |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON;
            } else {
                _config.dwCommonButtons &= ~TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON;
            }
        }
    }

    public bool YesButton {
        get {
            return (_config.dwCommonButtons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON) != 0;
        }
        set {
            if (value) {
                _config.dwCommonButtons |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON;
            } else {
                _config.dwCommonButtons &= ~TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON;
            }
        }
    }

    public bool NoButton {
        get {
            return (_config.dwCommonButtons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON) != 0;
        }
        set {
            if (value) {
                _config.dwCommonButtons |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON;
            } else {
                _config.dwCommonButtons &= ~TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON;
            }
        }
    }

    public bool CancelButton {
        get {
            return (_config.dwCommonButtons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON) != 0;
        }
        set {
            if (value) {
                _config.dwCommonButtons |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON;
            } else {
                _config.dwCommonButtons &= ~TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON;
            }
        }
    }

    public bool RetryButton {
        get {
            return (_config.dwCommonButtons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_RETRY_BUTTON) != 0;
        }
        set {
            if (value) {
                _config.dwCommonButtons |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_RETRY_BUTTON;
            } else {
                _config.dwCommonButtons &= ~TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_RETRY_BUTTON;
            }
        }
    }

    public bool CloseButton {
        get {
            return (_config.dwCommonButtons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON) != 0;
        }
        set {
            if (value) {
                _config.dwCommonButtons |= TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON;
            } else {
                _config.dwCommonButtons &= ~TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CLOSE_BUTTON;
            }
        }
    }

    public void ClearButtons() {
        _config.dwCommonButtons = 0;
    }

    // delayed
    public string? WindowTitle { get; set; }

    // delayed
    public string? MainInstruction { get; set; }

    // delayed
    public string? Content { get; set; }

    public int DefaultButton {
        get => _config.nDefaultButton;
        set => _config.nDefaultButton = value;
    }

    // delayed
    public string? Footer { get; set; }

    bool MainIconIsHICON {
        get {
            return (_config.dwFlags & TASKDIALOG_FLAGS.TDF_USE_HICON_MAIN) != 0;
        }
        set {
            if (value) {
                _config.dwFlags |= TASKDIALOG_FLAGS.TDF_USE_HICON_MAIN;
            } else {
                _config.dwFlags &= ~TASKDIALOG_FLAGS.TDF_USE_HICON_MAIN;
            }
        }
    }

    public HICON MainIcon {
        get {
            if (!MainIconIsHICON) {
                throw new InvalidOperationException("Icon is in PCWSTR mode");
            }
            return _config.hMainIcon;
        }
        set {
            MainIconIsHICON = true;
            _config.hMainIcon = value;
        }
    }

    public PCWSTR MainIconResource {
        get {
            if (MainIconIsHICON) {
                throw new InvalidOperationException("Icon is in HICON mode");
            }
            return _config.pszMainIcon;
        }
        set {
            MainIconIsHICON = false;
            _config.pszMainIcon = value;
        }
    }

    bool FooterIconIsHICON {
        get {
            return (_config.dwFlags & TASKDIALOG_FLAGS.TDF_USE_HICON_FOOTER) != 0;
        }
        set {
            if (value) {
                _config.dwFlags |= TASKDIALOG_FLAGS.TDF_USE_HICON_FOOTER;
            } else {
                _config.dwFlags &= ~TASKDIALOG_FLAGS.TDF_USE_HICON_FOOTER;
            }
        }
    }

    public HICON FooterIcon {
        get {
            if (!FooterIconIsHICON) {
                throw new InvalidOperationException("Icon is in PCWSTR mode");
            }
            return _config.hFooterIcon;
        }
        set {
            FooterIconIsHICON = true;
            _config.hFooterIcon = value;
        }
    }

    public PCWSTR FooterIconResource {
        get {
            if (FooterIconIsHICON) {
                throw new InvalidOperationException("Icon is in HICON mode");
            }
            return _config.pszFooterIcon;
        }
        set {
            FooterIconIsHICON = false;
            _config.pszFooterIcon = value;
        }
    }

    TaskDialogResult InternalShow(bool enableVerificationFlag, CancellationToken ct = default) {
        int button;
        int radioButton;
        BOOL verificationFlagChecked = false;

        unsafe {
            fixed (char* windowTitle = WindowTitle,
                mainInstruction = MainInstruction,
                content = Content,
                footer = Footer) {
                _config.pszWindowTitle = windowTitle;
                _config.pszMainInstruction = mainInstruction;
                _config.pszContent = content;
                _config.pszFooter = footer;
                try {
                    fixed (TASKDIALOGCONFIG* pConfig = &_config) {
                        PInvoke.TaskDialogIndirect(
                            pConfig,
                            &button,
                            &radioButton,
                            enableVerificationFlag ? &verificationFlagChecked : null).ThrowOnFailure();
                    }
                } finally {
                    _config.pszWindowTitle = null;
                    _config.pszMainInstruction = null;
                    _config.pszContent = null;
                    _config.pszFooter = null;
                }
            }
        }
        return new(button, radioButton, enableVerificationFlag && verificationFlagChecked);
    }

    public TaskDialogResult Show(bool enableVerificationFlag, CancellationToken ct = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy) {
            throw new InvalidOperationException("Task dialog is busy");
        }
        _busy = true;
        _cancelling = false;
        try {
            using var reg = ct.UnsafeRegister(_ => _cancelling = true, null);
            return InternalShow(enableVerificationFlag, ct);
        } finally {
            _busy = false;
        }
    }

    /// <summary>
    /// Unlike with <see cref="Window"/>, <see cref="Dispose"/> must be called manually.
    /// </summary>
    public virtual void Dispose() {
        if (_busy) {
            throw new InvalidOperationException("Task dialog is still running");
        }
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }
        _config.lpCallbackData = 0;
        _gch.Free();
    }
}
