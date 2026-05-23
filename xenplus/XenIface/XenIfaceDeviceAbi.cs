using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;

namespace XenPlus.XenIface;

sealed partial class XenIfaceDevice {
    // thanks CsWin32 for not defining these...

    const uint FILE_DEVICE_UNKNOWN = 0x00000022;
    const uint METHOD_BUFFERED = 0;
    const uint METHOD_NEITHER = 3;
    const uint FILE_ANY_ACCESS = 0;

    static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access) {
        return (DeviceType << 16) | (Access << 14) | (Function << 2) | Method;
    }

    const int XENSTORE_PAYLOAD_MAX = 4096;
    const int XENSTORE_ABS_PATH_MAX = 3072;
    const int XENSTORE_REL_PATH_MAX = 2048;

    static readonly uint IOCTL_XENIFACE_STORE_READ =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_WRITE =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_DIRECTORY =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_REMOVE =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x803, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_SET_PERMISSIONS =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x804, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_ADD_WATCH =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x805, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_STORE_REMOVE_WATCH =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x806, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x810, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x811, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_CLOSE =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x812, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_NOTIFY =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x813, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_EVTCHN_UNMASK =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x814, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x820, METHOD_NEITHER, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2 =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x824, METHOD_NEITHER, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x821, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2 =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x825, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x822, METHOD_NEITHER, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_V2 =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x826, METHOD_NEITHER, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x823, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES_V2 =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x827, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SUSPEND_GET_COUNT =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x830, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SUSPEND_REGISTER =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x831, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SUSPEND_DEREGISTER =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x832, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_SHAREDINFO_GET_TIME =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x840, METHOD_BUFFERED, FILE_ANY_ACCESS);
    static readonly uint IOCTL_XENIFACE_LOG =
        CTL_CODE(FILE_DEVICE_UNKNOWN, 0x84F, METHOD_BUFFERED, FILE_ANY_ACCESS);

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

    /// <summary>
    /// Put an ASCII string into a byte buffer, while ensuring that it's null-terminated.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <param name="buffer">Output buffer.</param>
    /// <param name="offset">Start offset of buffer to put string into.</param>
    /// <returns>Length of resulting byte string, without null terminator.</returns>
    static int FormatString(string? value, byte[] buffer, int offset = 0) {
        var len = value != null ? StoreEncoding.Instance.GetBytes(value, 0, value.Length, buffer, offset) : 0;
        if (offset + len >= buffer.Length) {
            throw new ArgumentException("value too long", nameof(value));
        }
        buffer[offset + len] = 0;
        return len;
    }

    internal string StoreRead(string path) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatString(path, inBuf, 0);
        var outBuf = new byte[XENSTORE_PAYLOAD_MAX];
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_READ, inBuf, outBuf)) {
                throw new Win32Exception();
            }
        }
        outBuf[^1] = 0;
        return StoreEncoding.Instance.GetString(outBuf, 0, outBuf.IndexOf<byte>(0));
    }

    internal void StoreWrite(string path, string? value) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        var pathLen = FormatString(path, inBuf, 0);
        FormatString(value, inBuf, pathLen + 1);
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_WRITE, inBuf)) {
                throw new Win32Exception();
            }
        }
    }

    internal List<string> StoreDirectory(string path) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatString(path, inBuf, 0);
        var outBuf = new byte[XENSTORE_PAYLOAD_MAX];
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_DIRECTORY, inBuf, outBuf)) {
                throw new Win32Exception();
            }
        }
        return Utils.ParseMultiString(outBuf, StoreEncoding.Instance.GetString);
    }

    internal void StoreRemove(string path) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatString(path, inBuf, 0);
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_REMOVE, inBuf)) {
                throw new Win32Exception();
            }
        }
    }

    internal WatchAbiHandle WatchAdd(string path, SafeWaitHandle evt) {
        var inPath = new byte[XENSTORE_PAYLOAD_MAX];
        var pathLen = FormatString(path, inPath, 0);
        unsafe {
            var outBuf = new XENIFACE_STORE_ADD_WATCH_OUT();
            fixed (byte* pathBytes = inPath) {
                var inBuf = new XENIFACE_STORE_ADD_WATCH_IN() {
                    Path = pathBytes,
                    PathLength = (uint)(pathLen + 1),
                    Event = (void*)evt.DangerousGetHandle()
                };
                if (!PInvoke.DeviceIoControl(
                    Handle,
                    IOCTL_XENIFACE_STORE_ADD_WATCH,
                    MemoryMarshal.AsBytes(new ReadOnlySpan<XENIFACE_STORE_ADD_WATCH_IN>(ref inBuf)),
                    MemoryMarshal.AsBytes(new Span<XENIFACE_STORE_ADD_WATCH_OUT>(ref outBuf)))) {
                    throw new Win32Exception();
                }
            }
            return new WatchAbiHandle(this, (nint)outBuf.Context);
        }
    }

    internal void WatchRemove(nint context) {
        // guard against stale watch handles
        if (Handle.IsInvalid) {
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
                throw new Win32Exception();
            }
        }
    }

    internal SuspendAbiHandle SuspendRegister(SafeWaitHandle evt) {
        unsafe {
            var outBuf = new XENIFACE_SUSPEND_REGISTER_OUT();
            var inBuf = new XENIFACE_SUSPEND_REGISTER_IN() {
                Event = (void*)evt.DangerousGetHandle()
            };
            if (!PInvoke.DeviceIoControl(
                Handle,
                IOCTL_XENIFACE_SUSPEND_REGISTER,
                MemoryMarshal.AsBytes(new ReadOnlySpan<XENIFACE_SUSPEND_REGISTER_IN>(ref inBuf)),
                MemoryMarshal.AsBytes(new Span<XENIFACE_SUSPEND_REGISTER_OUT>(ref outBuf)))) {
                throw new Win32Exception();
            }
            return new SuspendAbiHandle(this, (nint)outBuf.Context);
        }
    }

    internal void SuspendDeregister(nint context) {
        // guard against stale suspend handles
        if (Handle.IsInvalid) {
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
                throw new Win32Exception();
            }
        }
    }
}
