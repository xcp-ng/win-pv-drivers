namespace XenPlus.VolumeInfo;

/*
 * To recap the naming convention:
 * - VBD name: xvda, hda, etc. per Xen convention
 * - VBD number: the whole number associated with a VBD name, e.g. (1 << 28 | disk << 8 | partition)
 * - Disk number: the "disk" part of the VBD number
 * - OS device number: \\.\PhysicalDriveN per Windows notation (also IOCTL_STORAGE_GET_DEVICE_NUMBER)
 * - Target ID: a disk's literal target ID as reported by the bus driver (see IOCTL_SCSI_GET_ADDRESS)
 */
interface IDiskInformation {
    /// <remarks>
    /// <c>\\.\PhysicalDriveN</c>
    /// </remarks>
    uint OSDeviceNumber { get; init; }
    /// <remarks>
    /// <see cref="Windows.Win32.PInvoke.IOCTL_SCSI_GET_ADDRESS"/>
    /// </remarks>
    uint TargetId { get; init; }
    /// <remarks>
    /// the <c>disk</c> part of the VBD number
    /// </remarks>
    uint DiskNumber { get; }
}
