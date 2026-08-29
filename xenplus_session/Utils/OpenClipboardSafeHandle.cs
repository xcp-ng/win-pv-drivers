using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

sealed class OpenClipboardSafeHandle : SafeHandle {
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
}
