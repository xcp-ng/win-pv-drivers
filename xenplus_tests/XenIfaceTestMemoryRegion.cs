using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.System.Memory;
using Windows.Win32.System.ProcessStatus;

namespace XenPlus;

sealed class XenIfaceTestMemoryRegion : SafeHandleZeroOrMinusOneIsInvalid {
    readonly int _length;

    XenIfaceTestMemoryRegion(nint address, int length) : base(ownsHandle: true) {
        SetHandle(address);
        _length = length;
    }

    public nint Address => handle;
    public int Length => _length;

    public static XenIfaceTestMemoryRegion Reserve(int pages = 1) {
        return Allocate(pages, commit: false, PAGE_PROTECTION_FLAGS.PAGE_NOACCESS);
    }

    public static XenIfaceTestMemoryRegion AllocateCommitted(
        int pages = 1,
        PAGE_PROTECTION_FLAGS protection = PAGE_PROTECTION_FLAGS.PAGE_READWRITE) {
        return Allocate(pages, commit: true, protection);
    }

    public static XenIfaceTestMemoryRegion? TryCreateAt(nint address) {
        if (address == 0 || address % XenIfaceIoctlTestSupport.AllocationGranularity != 0) {
            return null;
        }
        nint allocated;
        unsafe {
            allocated = (nint)PInvokeT.VirtualAlloc(
                (void*)address,
                checked((nuint)XenIfaceIoctlTestSupport.PageSize),
                VIRTUAL_ALLOCATION_TYPE.MEM_RESERVE | VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT,
                PAGE_PROTECTION_FLAGS.PAGE_READWRITE);
        }
        if (allocated != address) {
            if (allocated != 0) {
                using var unexpected = new XenIfaceTestMemoryRegion(allocated, XenIfaceIoctlTestSupport.PageSize);
            }
            return null;
        }
        return new XenIfaceTestMemoryRegion(allocated, XenIfaceIoctlTestSupport.PageSize);
    }

    public static XenIfaceTestMemoryRegion AllocateUnmapped() {
        var region = AllocateCommitted();
        try {
            region.Unmap();
            return region;
        } catch {
            region.Dispose();
            throw;
        }
    }

    static XenIfaceTestMemoryRegion Allocate(int pages, bool commit, PAGE_PROTECTION_FLAGS protection) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var length = checked(pages * XenIfaceIoctlTestSupport.PageSize);
        var allocation = VIRTUAL_ALLOCATION_TYPE.MEM_RESERVE;
        if (commit) {
            allocation |= VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT;
        }
        nint address;
        unsafe {
            address = (nint)PInvokeT.VirtualAlloc(null, checked((nuint)length), allocation, protection);
            if (address == 0) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "VirtualAlloc failed");
            }
        }
        return new XenIfaceTestMemoryRegion(address, length);
    }

    public void Protect(PAGE_PROTECTION_FLAGS protection) {
        ProtectPage(0, protection);
    }

    public void ProtectPage(int pageIndex, PAGE_PROTECTION_FLAGS protection) {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, Length / XenIfaceIoctlTestSupport.PageSize);
        var address = checked(Address + pageIndex * XenIfaceIoctlTestSupport.PageSize);
        unsafe {
            var oldProtection = default(PAGE_PROTECTION_FLAGS);
            if (!PInvokeT.VirtualProtect(
                (void*)address,
                checked((nuint)XenIfaceIoctlTestSupport.PageSize),
                protection,
                &oldProtection)) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "VirtualProtect failed");
            }
        }
    }

    public void DecommitPage(int pageIndex = 0) {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, Length / XenIfaceIoctlTestSupport.PageSize);
        var address = checked(Address + pageIndex * XenIfaceIoctlTestSupport.PageSize);
        unsafe {
            if (!PInvokeT.VirtualFree(
                (void*)address,
                checked((nuint)XenIfaceIoctlTestSupport.PageSize),
                VIRTUAL_FREE_TYPE.MEM_DECOMMIT)) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "VirtualFree(MEM_DECOMMIT) failed");
            }
        }
    }

    public PAGE_PROTECTION_FLAGS QueryProtection(int offset = 0) {
        return Query(offset).Protect;
    }

    public VIRTUAL_ALLOCATION_TYPE QueryState(int offset = 0) {
        return Query(offset).State;
    }

    MEMORY_BASIC_INFORMATION Query(int offset) {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, Length);
        var information = default(MEMORY_BASIC_INFORMATION);
        unsafe {
            if (PInvokeT.VirtualQuery(
                (void*)(Address + offset),
                &information,
                (nuint)sizeof(MEMORY_BASIC_INFORMATION)) == 0) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "VirtualQuery failed");
            }
        }
        return information;
    }

    public unsafe byte ReadByte(int offset) {
        return Volatile.Read(ref *(byte*)(Address + offset));
    }

    public unsafe void WriteByte(int offset, byte value) {
        Volatile.Write(ref *(byte*)(Address + offset), value);
    }

    public unsafe bool TrimOutsideWorkingSet() {
        if (!PInvokeT.EmptyWorkingSet(PInvokeT.GetCurrentProcess())) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "EmptyWorkingSet failed");
        }
        var information = new PSAPI_WORKING_SET_EX_INFORMATION { VirtualAddress = (void*)Address };
        if (!PInvokeT.QueryWorkingSetEx(
            PInvokeT.GetCurrentProcess(),
            &information,
            (uint)sizeof(PSAPI_WORKING_SET_EX_INFORMATION))) {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "QueryWorkingSetEx failed");
        }
        return !information.VirtualAttributes.Valid;
    }

    void Unmap() {
        if (IsClosed || IsInvalid) {
            return;
        }
        unsafe {
            if (!PInvokeT.VirtualFree((void*)handle, 0, VIRTUAL_FREE_TYPE.MEM_RELEASE)) {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "VirtualFree failed");
            }
        }
        // Preserve Address for the stale-pointer test without letting SafeHandle free it twice.
        SetHandleAsInvalid();
    }

    protected override bool ReleaseHandle() {
        unsafe {
            return PInvokeT.VirtualFree((void*)handle, 0, VIRTUAL_FREE_TYPE.MEM_RELEASE);
        }
    }
}
