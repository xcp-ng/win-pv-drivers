using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Ole;
using XenPlus;

sealed class OpenClipboardSafeHandle : SafeHandle {
    static readonly uint ExcludeClipboardContentFromMonitorProcessing =
        PInvoke.RegisterClipboardFormat(nameof(ExcludeClipboardContentFromMonitorProcessing));
    static readonly uint CanIncludeInClipboardHistory =
        PInvoke.RegisterClipboardFormat(nameof(CanIncludeInClipboardHistory));
    static readonly uint CanUploadToCloudClipboard =
        PInvoke.RegisterClipboardFormat(nameof(CanUploadToCloudClipboard));

    public OpenClipboardSafeHandle(HWND hwnd) : base(nint.Zero, true) {
        if (!PInvoke.OpenClipboard(hwnd)) {
            throw new Win32Exception(nameof(PInvoke.OpenClipboard));
        }
        SetHandle(1);
    }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle() {
        return PInvoke.CloseClipboard();
    }

    public uint GetUInt32OrDefault(uint format, uint defaultValue) {
        ObjectDisposedException.ThrowIf(IsClosed || IsInvalid, this);

        if (format == 0) {
            throw new InvalidOperationException("Clipboard format is not registered");
        }
        if (!PInvoke.IsClipboardFormatAvailable(format)) {
            return defaultValue;
        }
        using var cb = ClipboardSafeHandle.GetClipboard((CLIPBOARD_FORMAT)format);
        if (!cb.TryGetUInt32(out var value)) {
            throw new InvalidOperationException($"Cannot read clipboard format {format}");
        }
        return value;
    }

    public bool IsExcludedFromMonitoring() {
        ObjectDisposedException.ThrowIf(IsClosed || IsInvalid, this);

        try {
            // presence alone means exclude, regardless of content
            if (ExcludeClipboardContentFromMonitorProcessing == 0 ||
                PInvoke.IsClipboardFormatAvailable(ExcludeClipboardContentFromMonitorProcessing)) {
                return true;
            }
            return GetUInt32OrDefault(CanIncludeInClipboardHistory, 1) == 0 ||
                GetUInt32OrDefault(CanUploadToCloudClipboard, 1) == 0;
        } catch {
            // fail closed
            return true;
        }
    }
}
