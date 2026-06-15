using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace XenPlus;

[Serializable]
sealed class WindowLockException : Exception {
    public WindowLockException() { }
    public WindowLockException(string message) : base(message) { }
    public WindowLockException(string message, Exception inner) : base(message, inner) { }
}

abstract class Window : IDisposable {
    readonly GCHandle _gch;
    readonly HWND _handle;
    bool _disposed = false;

    static readonly Lock _registeredClassesLock = new();
    static readonly Dictionary<string, ushort> _registeredClasses = [];

    public HWND Handle => _handle;

    static unsafe ushort RegisterClass(string className) {
        lock (_registeredClassesLock) {
            if (_registeredClasses.TryGetValue(className, out var atom)) {
                return atom;
            } else {
                fixed (char* classNamePtr = className) {
                    var wcex = new WNDCLASSEXW() {
                        cbSize = (uint)Unsafe.SizeOf<WNDCLASSEXW>(),
                        lpfnWndProc = &WndProcNative,
                        lpszClassName = classNamePtr,
                    };
                    atom = PInvoke.RegisterClassEx(wcex);
                    if (atom == 0) {
                        throw new Win32Exception(nameof(PInvoke.RegisterClassEx));
                    }
                    _registeredClasses[className] = atom;
                    return atom;
                }
            }
        }
    }

    public Window(string className, string windowName) {
        _gch = GCHandle.Alloc(this);

        unsafe {
            _ = RegisterClass(className);

            var createParam = (void*)GCHandle.ToIntPtr(_gch);
            Trace.TraceInformation("CreateWindowEx createParam={0:x}", (nint)createParam);
            _handle = PInvoke.CreateWindowEx(
                0,
                className,
                windowName,
                WINDOW_STYLE.WS_OVERLAPPED,
                0,
                0,
                0,
                0,
                HWND.Null,
                null,
                PInvoke.GetModuleHandle(null),
                createParam);
            if (_handle == HWND.Null) {
                throw new Win32Exception(nameof(PInvoke.CreateWindowEx));
            }
        }
    }

    static Window? GetSelf(HWND hwnd) {
        var selfHandle = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_USERDATA);
        Trace.TraceInformation("GetSelf selfHandle={0}", selfHandle);
        if (selfHandle == nint.Zero) {
            return null;
        }

        var gch = GCHandle.FromIntPtr(selfHandle);
        return gch.Target as Window;
    }

    static LRESULT OnCreateNative(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        try {
            Trace.TraceInformation("OnCreateNative lparam={0}", lparam);
            ArgumentOutOfRangeException.ThrowIfZero(lparam.Value);

            nint createParam;
            unsafe {
                var createStruct = (CREATESTRUCTW*)lparam.Value;
                createParam = (nint)createStruct->lpCreateParams;
            }
            Trace.TraceInformation("OnCreateNative createParam={0:x}", createParam);
            ArgumentOutOfRangeException.ThrowIfZero(createParam);

            var gch = GCHandle.FromIntPtr(createParam);
            var target = gch.Target as Window ?? throw new WindowLockException();
            /// <see cref="Handle"/> is not yet usable at this point because CreateWindow has not finished

            Marshal.SetLastPInvokeError(0);
            if (PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_USERDATA, createParam) == 0) {
                var err = Marshal.GetLastPInvokeError();
                if (err != 0) {
                    throw new Win32Exception(err, nameof(PInvoke.SetWindowLongPtr));
                }
            }
            return target.WndProc(hwnd, msg, wparam, lparam);
        } catch (Exception ex) {
            Trace.TraceError("Window creation failed: {0}", ex.ToString());
            return (LRESULT)(-1);
        }
    }

    LRESULT OnDestroyNative(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        try {
            WndProc(hwnd, msg, wparam, lparam);
        } catch (Exception ex) {
            Trace.TraceError("Window destruction failed: {0}", ex.ToString());
        }
        PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_USERDATA, nint.Zero);
        Dispose();
        return (LRESULT)0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static LRESULT WndProcNative(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        Trace.TraceInformation(
            "WndProcNative: {0} {1} {2} {3}",
            hwnd,
            msg,
            wparam,
            lparam);
        if (msg == PInvoke.WM_CREATE) {
            return OnCreateNative(hwnd, msg, wparam, lparam);
        } else {
            var target = GetSelf(hwnd);

            if (msg == PInvoke.WM_DESTROY) {
                return Check.Unwrap<Window>(target).OnDestroyNative(hwnd, msg, wparam, lparam);
            } else {
                if (target == null) {
                    Trace.TraceInformation(
                        "instance not found: {0} {1} {2} {3}",
                        hwnd,
                        msg,
                        wparam,
                        lparam);
                    return PInvoke.DefWindowProc(hwnd, msg, wparam, lparam);
                }
                return target.WndProc(hwnd, msg, wparam, lparam);
            }
        }
    }

    protected virtual LRESULT WndProc(HWND hwnd, uint msg, WPARAM wparam, LPARAM lparam) {
        return PInvoke.DefWindowProc(hwnd, msg, wparam, lparam);
    }

    /// <summary>
    /// <see cref="Dispose"/> will be called automatically during <see cref="PInvoke.WM_DESTROY"/>.
    /// </summary>
    public virtual void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }
        _gch.Free();
    }
}
