using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.Storage.IscsiDisc;
using Windows.Win32.System.Ioctl;

namespace XenPlus.VolumeInfo;

sealed class XenDiskInformation {
    // \\.\PhysicalDriveN
    public required uint DiskNumber { get; init; }
    public required uint TargetId { get; init; }
}

/// <remarks>
/// <para>
/// <see cref="XenDiskStore"/> is needed because it's not trivial to associate emulated storage adapters and their
/// targets to a vbd frontend. For one thing, there's not at all a promise of what an emulated storage controller looks
/// like.
/// </para>
/// <para>
/// Secondly, xenvbd also irreversibly maps the vbd device number to a target ID, which makes the target ID specific to
/// each adapter. Since the target ID does not equal the vbd device number from xenstore, we can't just start from a
/// list of disks; it's just easier to query xenvbd directly for a list of targets, match the target ID with what's in
/// xenstore to get the true device number, then use it to build the device name.
/// </para>
/// </remarks>
static class XenDiskStore {
    static string? GetXenVbdAdapter() {
        return Cfgmgr32.GetDeviceInstances(
            "XENBUS",
            PInvoke.CM_GETIDLIST_FILTER_ENUMERATOR | PInvoke.CM_GETIDLIST_FILTER_PRESENT)
            .FirstOrDefault(x => x.Contains("&DEV_VBD\\", StringComparison.OrdinalIgnoreCase));
    }

    static XenDiskInformation GetXenDisk(SafeHandle diskHandle) {
        var devNumber = new STORAGE_DEVICE_NUMBER();
        unsafe {
            if (!PInvoke.DeviceIoControl(
                diskHandle,
                PInvoke.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                null,
                MemoryMarshal.AsBytes(new Span<STORAGE_DEVICE_NUMBER>(ref devNumber)))) {
                throw new Win32Exception(nameof(PInvoke.IOCTL_STORAGE_GET_DEVICE_NUMBER));
            }
        }
        if (devNumber.DeviceType != (uint)FILE_DEVICE_TYPE.FILE_DEVICE_DISK) {
            throw new NotSupportedException($"Unsupported device type {devNumber.DeviceType}");
        }
        if (devNumber.DeviceNumber == unchecked((uint)-1)) {
            throw new NotSupportedException($"Disk unexpectedly has no number");
        }

        var address = new SCSI_ADDRESS();
        unsafe {
            if (!PInvoke.DeviceIoControl(
                diskHandle,
                PInvoke.IOCTL_SCSI_GET_ADDRESS,
                null,
                MemoryMarshal.AsBytes(new Span<SCSI_ADDRESS>(ref address)))) {
                throw new Win32Exception(nameof(PInvoke.IOCTL_SCSI_GET_ADDRESS));
            }
        }

        return new XenDiskInformation() {
            DiskNumber = devNumber.DeviceNumber,
            TargetId = address.TargetId,
        };
    }

    internal static IEnumerable<XenDiskInformation> GetXenDisks() {
        var xenvbd = GetXenVbdAdapter();
        if (string.IsNullOrEmpty(xenvbd)) {
            yield break;
        }

        uint xenvbdInst;
        unsafe {
            fixed (char* xenvbdPtr = xenvbd) {
                Cfgmgr32.CheckConfigret(PInvoke.CM_Locate_DevNode(
                    out xenvbdInst,
                    xenvbdPtr,
                    CM_LOCATE_DEVNODE_FLAGS.CM_LOCATE_DEVNODE_NORMAL));
            }
        }

        foreach (var xendisk in Cfgmgr32.GetChildren(xenvbdInst)) {
            string disk;
            try {
                disk = Cfgmgr32.GetDeviceInterfaces(PInvoke.GUID_DEVINTERFACE_DISK, xendisk).Single();
            } catch {
                // ???, but move on to the next anyway
                continue;
            }

            using var diskHandle = File.OpenHandle(
                disk,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            yield return GetXenDisk(diskHandle);
        }
    }
}
