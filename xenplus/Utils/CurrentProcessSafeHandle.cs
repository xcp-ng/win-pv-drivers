using System.Runtime.InteropServices;
using Windows.Win32;

namespace XenPlus;

/// <summary>
/// https://github.com/microsoft/CsWin32/issues/1411
/// </summary>
sealed class CurrentProcessSafeHandle : SafeHandle {

    public CurrentProcessSafeHandle() : base(nint.Zero, false) {
        SetHandle(PInvoke.GetCurrentProcess());
    }

    public override bool IsInvalid => DangerousGetHandle() == nint.Zero;

    protected override bool ReleaseHandle() {
        return true;
    }
}
