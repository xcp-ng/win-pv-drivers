using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;

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

    internal string StoreRead(string path, bool strict) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatString(path, inBuf, 0, strict);
        var outBuf = new byte[XENSTORE_PAYLOAD_MAX];
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_READ, inBuf, outBuf)) {
                throw new Win32Exception();
            }
        }
        outBuf[^1] = 0;
        return GetEncoding(strict).GetString(outBuf, 0, outBuf.IndexOf<byte>(0));
    }

    internal void StoreWrite(string path, string? value, bool strict) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        var pathLen = FormatString(path, inBuf, 0, strict);
        FormatString(value, inBuf, pathLen + 1, strict);
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_WRITE, inBuf)) {
                throw new Win32Exception();
            }
        }
    }

    internal List<string> StoreDirectory(string path, bool strict) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatString(path, inBuf, 0, strict);
        var outBuf = new byte[XENSTORE_PAYLOAD_MAX];
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_DIRECTORY, inBuf, outBuf)) {
                throw new Win32Exception();
            }
        }
        return Utils.ParseMultiString(outBuf, GetEncoding(strict).GetString);
    }

    internal void StoreRemove(string path, bool strict) {
        var inBuf = new byte[XENSTORE_PAYLOAD_MAX];
        FormatString(path, inBuf, 0, strict);
        unsafe {
            if (!PInvoke.DeviceIoControl(Handle, IOCTL_XENIFACE_STORE_REMOVE, inBuf)) {
                throw new Win32Exception();
            }
        }
    }

    internal WatchAbiHandle WatchAdd(string path, SafeWaitHandle evt, bool strict) {
        var inPath = new byte[XENSTORE_PAYLOAD_MAX];
        var pathLen = FormatString(path, inPath, 0, strict);
        unsafe {
            var outBuf = new XENIFACE_STORE_ADD_WATCH_OUT();
            fixed (byte* pathBytes = inPath) {
                bool addRef = false;
                evt.DangerousAddRef(ref addRef);
                try {
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
                } finally {
                    if (addRef) {
                        evt.DangerousRelease();
                    }
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
                throw new Win32Exception();
            }
        }
    }

    internal SuspendAbiHandle SuspendRegister(SafeWaitHandle evt) {
        unsafe {
            bool addRef = false;
            evt.DangerousAddRef(ref addRef);
            try {
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
            } finally {
                if (addRef) {
                    evt.DangerousRelease();
                }
            }
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
                throw new Win32Exception();
            }
        }
    }
}
