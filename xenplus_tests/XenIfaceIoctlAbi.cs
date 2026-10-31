using System.Runtime.InteropServices;
using Windows.Win32;

namespace XenPlus;

static class XenIfaceIoctl {
    static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access) {
        return (DeviceType << 16) | (Access << 14) | (Function << 2) | Method;
    }

    public static readonly uint IOCTL_XENIFACE_STORE_READ = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x800, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_STORE_WRITE = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x801, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_STORE_REMOVE = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x803, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x810, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x811, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_EVTCHN_CLOSE = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x812, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_EVTCHN_NOTIFY = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x813, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_EVTCHN_UNMASK = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x814, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x820, PInvokeT.METHOD_NEITHER, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x821, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x822, PInvokeT.METHOD_NEITHER, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x823, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2 = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x824, PInvokeT.METHOD_NEITHER, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2 = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x825, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_V2 = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x826, PInvokeT.METHOD_NEITHER, PInvokeT.FILE_ANY_ACCESS);
    public static readonly uint IOCTL_XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES_V2 = CTL_CODE(
        PInvokeT.FILE_DEVICE_UNKNOWN, 0x827, PInvokeT.METHOD_BUFFERED, PInvokeT.FILE_ANY_ACCESS);
}

[Flags]
enum XENIFACE_GNTTAB_PAGE_FLAGS {
    XENIFACE_GNTTAB_READONLY = 1 << 0,
    XENIFACE_GNTTAB_USE_NOTIFY_OFFSET = 1 << 1,
    XENIFACE_GNTTAB_USE_NOTIFY_PORT = 1 << 2,
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_EVTCHN_BIND_INTERDOMAIN_IN {
    public ushort RemoteDomain;
    public uint RemotePort;
    public byte Mask;
    public nint Event;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_EVTCHN_BIND_INTERDOMAIN_OUT {
    public uint LocalPort;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_EVTCHN_BIND_UNBOUND_IN {
    public ushort RemoteDomain;
    public byte Mask;
    public nint Event;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_EVTCHN_BIND_UNBOUND_OUT {
    public uint LocalPort;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_EVTCHN_CLOSE_IN {
    public uint LocalPort;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_EVTCHN_NOTIFY_IN {
    public uint LocalPort;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_EVTCHN_UNMASK_IN {
    public uint LocalPort;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN {
    public uint RequestId;
    public ushort RemoteDomain;
    public uint NumberPages;
    public XENIFACE_GNTTAB_PAGE_FLAGS Flags;
    public uint NotifyOffset;
    public uint NotifyPort;
}

/// <summary>Header layout includes References[ANYSIZE_ARRAY]; output size extends from its field offset.</summary>
[StructLayout(LayoutKind.Sequential)]
unsafe struct XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_OUT {
    public nint Address;
    public fixed uint References[1];
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2() {
    public ushort RemoteDomain;
    public nint Address;
    public uint NumberPages;
    public XENIFACE_GNTTAB_PAGE_FLAGS Flags;
    public uint NotifyOffset;
    public uint NotifyPort;
}

/// <summary>Alias layout for XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_OUT.</summary>
[StructLayout(LayoutKind.Sequential)]
unsafe struct XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_OUT_V2 {
    public nint Address;
    public fixed uint References[1];
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN {
    public uint RequestId;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2 {
    public nint Address;
}

/// <summary>Header layout includes References[ANYSIZE_ARRAY]; input size extends from its field offset.</summary>
[StructLayout(LayoutKind.Sequential)]
unsafe struct XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_IN {
    public uint RequestId;
    public ushort RemoteDomain;
    public uint NumberPages;
    public XENIFACE_GNTTAB_PAGE_FLAGS Flags;
    public uint NotifyOffset;
    public uint NotifyPort;
    public fixed uint References[1];
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_OUT {
    public nint Address;
}

/// <summary>Header layout includes References[ANYSIZE_ARRAY]; input size extends from its field offset.</summary>
[StructLayout(LayoutKind.Sequential)]
unsafe struct XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_IN_V2 {
    public ushort RemoteDomain;
    public uint NumberPages;
    public XENIFACE_GNTTAB_PAGE_FLAGS Flags;
    public uint NotifyOffset;
    public uint NotifyPort;
    public fixed uint References[1];
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_OUT_V2 {
    public nint Address;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES_IN(uint requestId) {
    public uint RequestId = requestId;
}

[StructLayout(LayoutKind.Sequential)]
struct XENIFACE_GNTTAB_UNMAP_FOREIGN_PAGES_IN_V2 {
    public nint Address;
}

static class XenIfaceIoctlAbi {
    public static int ArrayLength(int fieldOffset, uint count, int elementSize) {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementSize);
        return checked(fieldOffset + (int)count * elementSize);
    }

    // Marshal.OffsetOf uses runtime marshalling, which is disabled for this assembly, so compute the field
    // offsets from the actual struct layout instead.
    public static unsafe int PermitReferencesOffset {
        get {
            XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_OUT value = default;
            return (int)((byte*)value.References - (byte*)&value);
        }
    }

    public static unsafe int MapV1ReferencesOffset {
        get {
            XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_IN value = default;
            return (int)((byte*)value.References - (byte*)&value);
        }
    }

    public static unsafe int MapV2ReferencesOffset {
        get {
            XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_IN_V2 value = default;
            return (int)((byte*)value.References - (byte*)&value);
        }
    }

    public static int PermitOutputLength(uint count) {
        return ArrayLength(PermitReferencesOffset, count, sizeof(uint));
    }

    public static int MapV1InputLength(uint count) {
        return ArrayLength(MapV1ReferencesOffset, count, sizeof(uint));
    }

    public static int MapV2InputLength(uint count) {
        return ArrayLength(MapV2ReferencesOffset, count, sizeof(uint));
    }
}
