using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Ole;

namespace XenPlus;

/// <summary>
/// Must only be used when clipboard is open.
/// </summary>
sealed class ClipboardSafeHandle(HGLOBAL h, CLIPBOARD_FORMAT format, bool ownsHandle)
#pragma warning disable CS9107
    : GlobalFreeSafeHandle(h, ownsHandle) {
#pragma warning restore CS9107
    public string GetString(int maxLength = int.MaxValue) {
        ObjectDisposedException.ThrowIf(IsInvalid, this);
        if (format != CLIPBOARD_FORMAT.CF_UNICODETEXT) {
            throw new InvalidOperationException($"Invalid clipboard format '{format}'");
        }
        bool addRef = false;
        DangerousAddRef(ref addRef);
        try {
            unsafe {
                var locked = PInvoke.GlobalLock(this);
                if (locked == null) {
                    throw new Win32Exception(nameof(PInvoke.GlobalLock));
                }
                try {
                    var size = PInvoke.GlobalSize(this);
                    if (size == 0) {
                        throw new Win32Exception(nameof(PInvoke.GlobalSize));
                    }
                    if (size > int.MaxValue) {
                        throw new OutOfMemoryException("clipboard string too long");
                    }
                    if ((int)size / sizeof(char) > maxLength) {
                        throw new OutOfMemoryException("clipboard string too long");
                    }

                    var chars = new Span<char>(locked, (int)size / sizeof(char));
                    var zero = chars.IndexOf('\0');
                    if (zero >= 0) {
                        chars = chars[..zero];
                    }

                    return new string(chars);
                } finally {
                    PInvoke.GlobalUnlock(this);
                }
            }
        } finally {
            if (addRef) {
                DangerousRelease();
            }
        }
    }

    public static ClipboardSafeHandle CreateString(ReadOnlySpan<char> value) {
        unsafe {
            var valueByteCount = value.Length * sizeof(char);
            var bufferByteCount = (value.Length + 1) * sizeof(char);

            var hglobal = PInvoke.GlobalAlloc(
                GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT,
                (nuint)bufferByteCount);
            if (hglobal.IsNull) {
                throw new Win32Exception(nameof(PInvoke.GlobalAlloc));
            }

            var result = new ClipboardSafeHandle(hglobal, CLIPBOARD_FORMAT.CF_UNICODETEXT, true);

            try {
                var locked = PInvoke.GlobalLock(result);
                if (locked == null) {
                    throw new Win32Exception(nameof(PInvoke.GlobalLock));
                }
                try {
                    int v = value.Length * sizeof(char);
                    fixed (char* p = value) {
                        Buffer.MemoryCopy(p, locked, bufferByteCount, valueByteCount);
                    }
                } finally {
                    PInvoke.GlobalUnlock(result);
                }
            } catch {
                result.Dispose();
                throw;
            }

            return result;
        }
    }

    public static ClipboardSafeHandle GetClipboard(CLIPBOARD_FORMAT format) {
        unsafe {
            return new((HGLOBAL)PInvoke.GetClipboardData((uint)format).Value, format, false);
        }
    }

    /// <summary>
    /// Consumes the handle.
    /// </summary>
    public void SetClipboard() {
        using var shref = this.Borrow();
        ObjectDisposedException.ThrowIf(IsClosed || IsInvalid, this);
        if (!ownsHandle) {
            throw new InvalidOperationException("cannot set clipboard with unowned handle");
        }
        if (PInvoke.SetClipboardData((uint)format, (HANDLE)shref.DangerousHandle) == HANDLE.Null) {
            throw new Win32Exception(nameof(PInvoke.SetClipboardData));
        }
        // SetClipboardData gives ownership of the handle over to Windows.
        SetHandleAsInvalid();
        // as we've borrowed, we must release the reference even if we've called SetHandleAsInvalid
    }
}
