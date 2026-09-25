using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Ole;

namespace XenPlus;

sealed class ClipboardSafeHandle : GlobalFreeSafeHandle {
    readonly CLIPBOARD_FORMAT _format;
    readonly bool _ownsHandle;
    readonly SafeHandleReferenceScope _openScope;

    ClipboardSafeHandle(
        SafeHandleReferenceScope open,
        HGLOBAL h,
        CLIPBOARD_FORMAT format,
        bool ownsHandle) : base(h, ownsHandle) {
        _format = format;
        _ownsHandle = ownsHandle;
        _openScope = open;
    }

    public string GetString(int maxLength = int.MaxValue) {
        ObjectDisposedException.ThrowIf(IsInvalid, this);
        if (_format != CLIPBOARD_FORMAT.CF_UNICODETEXT) {
            throw new InvalidOperationException($"Invalid clipboard format '{_format}'");
        }
        using var shref = this.Borrow();
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
    }

    public static ClipboardSafeHandle CreateString(OpenClipboardSafeHandle open, ReadOnlySpan<char> value) {
        SafeHandleReferenceScope? scope = null;
        ClipboardSafeHandle? result = null;

        try {
            scope = open.Borrow();

            var valueByteCount = value.Length * sizeof(char);
            var bufferByteCount = (value.Length + 1) * sizeof(char);

            var hglobal = PInvoke.GlobalAlloc(
                GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT,
                (nuint)bufferByteCount);
            if (hglobal.IsNull) {
                throw new Win32Exception(nameof(PInvoke.GlobalAlloc));
            }

            result = new(scope, hglobal, CLIPBOARD_FORMAT.CF_UNICODETEXT, true);
            scope = null; // consumed by result
            unsafe {
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
            }

            return result;
        } catch {
            result?.Dispose();
            scope?.Dispose();
            throw;
        }
    }

    public static ClipboardSafeHandle GetClipboard(OpenClipboardSafeHandle open, CLIPBOARD_FORMAT format) {
        unsafe {
            return new(open.Borrow(), (HGLOBAL)PInvoke.GetClipboardData((uint)format).Value, format, false);
        }
    }

    /// <summary>
    /// Consumes the handle.
    /// </summary>
    public void SetClipboard() {
        using var shref = this.Borrow();
        ObjectDisposedException.ThrowIf(IsClosed || IsInvalid, this);
        if (!_ownsHandle) {
            throw new InvalidOperationException("cannot set clipboard with unowned handle");
        }
        if (PInvoke.SetClipboardData((uint)_format, (HANDLE)shref.Handle) == HANDLE.Null) {
            throw new Win32Exception(nameof(PInvoke.SetClipboardData));
        }
        // SetClipboardData gives ownership of the handle over to Windows.
        SetHandleAsInvalid();
        // as we've borrowed, we must release the reference even if we've called SetHandleAsInvalid
    }

    protected override bool ReleaseHandle() {
        _openScope.Dispose();
        return base.ReleaseHandle();
    }
}
