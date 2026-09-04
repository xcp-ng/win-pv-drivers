using System.Runtime.InteropServices;

namespace XenPlus;

public sealed class SafeHandleReferenceScope : IDisposable {
    readonly SafeHandle _h;
    bool _ref = false;
#if DEBUG
    bool _disposed = false;

    void Validate() {
        Check.DebugAssert(!_disposed);
    }
#endif

    public SafeHandleReferenceScope(SafeHandle h) {
        _h = h;
        _h.DangerousAddRef(ref _ref);
    }

    public nint Handle {
        get {
#if DEBUG
            Validate();
#endif
            ObjectDisposedException.ThrowIf(!_ref, this);
            if (_h.IsInvalid) {
                throw new InvalidOperationException("tried to acquire handle from invalid SafeHandle");
            }
            return _h.DangerousGetHandle();
        }
    }

    public void Dispose() {
#if DEBUG
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }
#endif
        var addRef = Interlocked.Exchange(ref _ref, false);
        if (addRef) {
            _h.DangerousRelease();
        }
    }
}

public static class SafeHandleExtensions {
    public static SafeHandleReferenceScope Borrow(this SafeHandle h) {
        return new(h);
    }
}
