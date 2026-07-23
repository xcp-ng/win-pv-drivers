using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace XenPlus.XenIface;

sealed partial class XenIfaceDevice {
    static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access) {
        return (DeviceType << 16) | (Access << 14) | (Function << 2) | Method;
    }

    const int XENSTORE_PAYLOAD_MAX = 4096;
    const int XENSTORE_ABS_PATH_MAX = 3072;
    const int XENSTORE_REL_PATH_MAX = 2048;

    static readonly uint IOCTL_XENIFACE_STORE_READ =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x800, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_WRITE =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x801, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_DIRECTORY =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x802, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_REMOVE =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x803, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_SET_PERMISSIONS =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x804, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_ADD_WATCH =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x805, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_REMOVE_WATCH =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x806, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x810, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x811, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_CLOSE =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x812, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_NOTIFY =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x813, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_UNMASK =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x814, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x820, PInvoke.METHOD_NEITHER, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2 =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x824, PInvoke.METHOD_NEITHER, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x821, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2 =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x825, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x822, PInvoke.METHOD_NEITHER, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_V2 =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x826, PInvoke.METHOD_NEITHER, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x823, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES_V2 =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x827, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SUSPEND_GET_COUNT =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x830, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SUSPEND_REGISTER =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x831, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SUSPEND_DEREGISTER =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x832, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SHAREDINFO_GET_TIME =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x840, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_LOG =
        CTL_CODE(PInvoke.FILE_DEVICE_UNKNOWN, 0x84F, PInvoke.METHOD_BUFFERED, PInvoke.FILE_ANY_ACCESS);

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct XENIFACE_STORE_ADD_WATCH_IN {
        public byte* Path;
        public uint PathLength;
        public void* Event;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct XENIFACE_STORE_ADD_WATCH_OUT {
        public void* Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct XENIFACE_SUSPEND_REGISTER_IN {
        public void* Event;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct XENIFACE_SUSPEND_REGISTER_OUT {
        public void* Context;
    }

    static Encoding GetEncoding(bool strict) {
        return strict ? StrictStoreEncoding.Instance : StoreEncoding.Instance;
    }

    /// <summary>
    /// Put an ASCII string into a byte buffer, while ensuring that it's null-terminated.
    /// </summary>
    /// <param name="strict">Use strict encoding.</param>
    /// <param name="value">Input string.</param>
    /// <param name="buffer">Output buffer.</param>
    /// <param name="offset">Start offset of buffer to put string into.</param>
    /// <returns>Length of resulting byte string, without null terminator.</returns>
    static int FormatString(string? value, byte[] buffer, int offset, bool strict) {
        var len = value != null ? GetEncoding(strict).GetBytes(value, 0, value.Length, buffer, offset) : 0;
        if (offset + len >= buffer.Length) {
            throw new ArgumentException("value too long", nameof(value));
        }
        buffer[offset + len] = 0;
        return len;
    }

    static void ValidatePath(string? value) {
        if (value == null) {
            return;
        }
        for (var i = 0; i < value.Length; i++) {
            char c = value[i];
            if (!((c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '-' || c == '/' || c == '_')) {
                throw new ArgumentException($"detected invalid XenStore path character at offset {i}");
            }
        }
        if (value.EndsWith('/')) {
            throw new ArgumentException($"trailing path slash is not allowed");
        }
        var doubleSlash = value.IndexOf("//");
        if (doubleSlash >= 0) {
            throw new ArgumentException($"found unacceptable double slash at offset {doubleSlash}");
        }
    }

    static int FormatPath(string? value, byte[] buffer, int offset) {
        if (value != null) {
            ValidatePath(value);
        }
        return FormatString(value, buffer, offset, true);
    }

    internal string? StoreTryRead(string path, bool strict) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatPath(path, inBuf, 0);
        var outBuf = new byte[XENSTORE_PAYLOAD_MAX];
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_READ, inBuf, outBuf)) {
                var err = Marshal.GetLastPInvokeError();
                if (err == (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND) {
                    return null;
                }
                throw new Win32Exception(err, nameof(IOCTL_XENIFACE_STORE_READ));
            }
        }
        outBuf[^1] = 0;
        return GetEncoding(strict).GetString(outBuf, 0, outBuf.IndexOf<byte>(0));
    }

    internal void StoreWrite(string path, string? value, bool strict) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        var pathLen = FormatPath(path, inBuf, 0);
        FormatString(value, inBuf, pathLen + 1, strict);
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_WRITE, inBuf)) {
                throw new Win32Exception(nameof(IOCTL_XENIFACE_STORE_WRITE));
            }
        }
    }

    /// <summary>
    /// Values from StoreDirectory are always taken as paths and are strictly checked.
    /// </summary>
    internal List<string>? StoreTryDirectory(string path) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatPath(path, inBuf, 0);
        var outBuf = new byte[XENSTORE_PAYLOAD_MAX];
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_DIRECTORY, inBuf, outBuf)) {
                var err = Marshal.GetLastPInvokeError();
                if (err == (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND) {
                    return null;
                }
                if (err == (int)WIN32_ERROR.ERROR_PATH_NOT_FOUND) {
                    // This is the error code for "this is a leaf value".
                    // FWIW, xenstore doesn't formally distinguish leaves and nodes. So just return an empty list.
                    return [];
                }
                throw new Win32Exception(nameof(IOCTL_XENIFACE_STORE_DIRECTORY));
            }
        }
        return ServerUtils.ParseMultiString(outBuf, GetEncoding(true).GetString)
            .Select(x => { ValidatePath(x); return x; })
            .ToList();
    }

    internal void StoreRemove(string path) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatPath(path, inBuf, 0);
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_REMOVE, inBuf)) {
                throw new Win32Exception(nameof(IOCTL_XENIFACE_STORE_REMOVE));
            }
        }
    }

    internal WatchAbiHandle WatchAdd(string path, SafeWaitHandle evt) {
        var inPath = new byte[XENSTORE_PAYLOAD_MAX];
        var pathLen = FormatPath(path, inPath, 0);
        unsafe {
            var outBuf = new XENIFACE_STORE_ADD_WATCH_OUT();
            fixed (byte* pathBytes = inPath) {
                using var shref = evt.Borrow();
                var inBuf = new XENIFACE_STORE_ADD_WATCH_IN() {
                    Path = pathBytes,
                    PathLength = (uint)(pathLen + 1),
                    Event = (void*)shref.DangerousHandle
                };
                if (!PInvoke.DeviceIoControl(
                    Handle,
                    IOCTL_XENIFACE_STORE_ADD_WATCH,
                    MemoryMarshal.AsBytes(new ReadOnlySpan<XENIFACE_STORE_ADD_WATCH_IN>(ref inBuf)),
                    MemoryMarshal.AsBytes(new Span<XENIFACE_STORE_ADD_WATCH_OUT>(ref outBuf)))) {
                    throw new Win32Exception(nameof(IOCTL_XENIFACE_STORE_ADD_WATCH));
                }
            }
            return new WatchAbiHandle(this, (nint)outBuf.Context);
        }
    }

    internal void WatchRemove(nint context) {
        // guard against stale watch handles
        if (Handle.IsInvalid || Handle.IsClosed) {
            return;
        }
        unsafe {
            var inBuf = new XENIFACE_STORE_ADD_WATCH_OUT() {
                Context = (void*)context
            };
            if (!PInvoke.DeviceIoControl(
                Handle,
                IOCTL_XENIFACE_STORE_REMOVE_WATCH,
                MemoryMarshal.AsBytes(new ReadOnlySpan<XENIFACE_STORE_ADD_WATCH_OUT>(ref inBuf)))) {
                throw new Win32Exception(nameof(IOCTL_XENIFACE_STORE_REMOVE_WATCH));
            }
        }
    }

    internal SuspendAbiHandle SuspendRegister(SafeWaitHandle evt) {
        unsafe {
            using var shref = evt.Borrow();
            var outBuf = new XENIFACE_SUSPEND_REGISTER_OUT();
            var inBuf = new XENIFACE_SUSPEND_REGISTER_IN() {
                Event = (void*)shref.DangerousHandle
            };
            if (!PInvoke.DeviceIoControl(
                Handle,
                IOCTL_XENIFACE_SUSPEND_REGISTER,
                MemoryMarshal.AsBytes(new ReadOnlySpan<XENIFACE_SUSPEND_REGISTER_IN>(ref inBuf)),
                MemoryMarshal.AsBytes(new Span<XENIFACE_SUSPEND_REGISTER_OUT>(ref outBuf)))) {
                throw new Win32Exception(nameof(IOCTL_XENIFACE_SUSPEND_REGISTER));
            }
            return new SuspendAbiHandle(this, (nint)outBuf.Context);
        }
    }

    internal void SuspendDeregister(nint context) {
        // guard against stale suspend handles
        if (Handle.IsInvalid || Handle.IsClosed) {
            return;
        }
        unsafe {
            var inBuf = new XENIFACE_SUSPEND_REGISTER_OUT() {
                Context = (void*)context
            };
            if (!PInvoke.DeviceIoControl(
                Handle,
                IOCTL_XENIFACE_SUSPEND_DEREGISTER,
                MemoryMarshal.AsBytes(new ReadOnlySpan<XENIFACE_SUSPEND_REGISTER_OUT>(ref inBuf)))) {
                throw new Win32Exception(nameof(IOCTL_XENIFACE_SUSPEND_DEREGISTER));
            }
        }
    }
}
