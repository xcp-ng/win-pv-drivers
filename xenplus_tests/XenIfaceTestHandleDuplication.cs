using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace XenPlus;

static class XenIfaceTestHandleDuplication {
    public static SafeFileHandle DuplicateFile(SafeHandle source) {
        return new(Duplicate(source, 0, sameAccess: true), ownsHandle: true);
    }

    public static SafeWaitHandle DuplicateEvent(SafeHandle source, uint access, bool sameAccess) {
        return new(Duplicate(source, access, sameAccess), ownsHandle: true);
    }

    static unsafe nint Duplicate(SafeHandle source, uint desiredAccess, bool sameAccess) {
        using var reference = source.Borrow();
        var process = PInvokeT.GetCurrentProcess();
        var original = new HANDLE(reference.Handle);
        var duplicate = default(HANDLE);
        var options = sameAccess ? DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS : 0;
        if (!PInvokeT.DuplicateHandle(process, original, process, &duplicate, desiredAccess, false, options)) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "DuplicateHandle failed");
        }
        return (nint)duplicate.Value;
    }
}
