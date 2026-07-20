using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Ioctl;

namespace XenPlus.VolumeInfo;

sealed class VolumeExtent {
    public required uint DiskNumber { get; init; }
    public required long StartingOffset { get; init; }
    public required long ExtentLength { get; init; }
}

sealed class VolumeInformation {
    public required string ObjectPath { get; init; }
    public required IReadOnlyList<string> MountPoints { get; init; }
    public required string Label { get; init; }
    public required string Filesystem { get; init; }
    public required ulong Size { get; init; }
    public required ulong Free { get; init; }
    public required IReadOnlyList<VolumeExtent> Extents { get; init; }
}


static class VolumeStore {
    static IEnumerable<string> GetVolumes() {
        var volumeName = new char[PInvoke.MAX_PATH + 1];
        using var findVolume = PInvoke.FindFirstVolume(volumeName.AsSpan());
        if (findVolume.IsInvalid) {
            throw new Win32Exception(nameof(PInvoke.FindFirstVolume));
        }

        while (true) {
            yield return new(volumeName[..volumeName.IndexOf('\0')]);
            try {
                using var borrow = findVolume.Borrow();
                if (!PInvoke.FindNextVolume((HANDLE)borrow.DangerousHandle, volumeName.AsSpan())) {
                    throw new Win32Exception(nameof(PInvoke.FindNextVolume));
                }
            } catch (Win32Exception ex) when (ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_NO_MORE_FILES) {
                yield break;
            }
        }
    }

    static IEnumerable<string> GetVolumePaths(string volumeName) {
        char[]? buffer = null;
        uint len;

        while (true) {
            unsafe {
                fixed (char* bufferPtr = buffer) {
                    if (PInvoke.GetVolumePathNamesForVolumeName(
                        volumeName,
                        bufferPtr,
                        (uint)(buffer?.Length ?? 0),
                        out len)) {
                        break;
                    }
                }
            }
            var err = Marshal.GetLastPInvokeError();
            if (err != (int)WIN32_ERROR.ERROR_MORE_DATA) {
                throw new Win32Exception(err, nameof(PInvoke.GetVolumePathNamesForVolumeName));
            }
            buffer = new char[len];
        }

        if (buffer == null) {
            yield break;
        }
        foreach (var path in ServerUtils.ParseMultiString(buffer.AsSpan()[..(int)len])) {
            yield return path;
        }
    }

    static IEnumerable<DISK_EXTENT> GetDiskExtents(SafeHandle volumeHandle) {
        var buffer = new byte[VOLUME_DISK_EXTENTS.SizeOf(1)];

        while (true) {
            unsafe {
                if (PInvoke.DeviceIoControl(
                    volumeHandle,
                    PInvoke.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
                    null,
                    buffer,
                    out _)) {
                    break;
                }
            }
            var err = Marshal.GetLastPInvokeError();
            if (err != (int)WIN32_ERROR.ERROR_MORE_DATA) {
                throw new Win32Exception(err, nameof(PInvoke.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS));
            }
            // here the resulting size may be smaller than sizeof(VOLUME_DISK_EXTENTS) (i.e. just the header).
            // take the whole buffer anyway because we are only reading NumberOfDiskExtents, but
            // MemoryMarshal.Read<VOLUME_DISK_EXTENTS> expects at least one full sizeof(VOLUME_DISK_EXTENTS)
            var temp = MemoryMarshal.Read<VOLUME_DISK_EXTENTS>(buffer);
            buffer = new byte[VOLUME_DISK_EXTENTS.SizeOf((int)temp.NumberOfDiskExtents)];
        }

        var header = MemoryMarshal.Read<VOLUME_DISK_EXTENTS>(buffer);
        if (header.NumberOfDiskExtents < 1) {
            yield break;
        }
        yield return header.Extents[0];
        for (int i = 1; i < header.NumberOfDiskExtents; i++) {
            var start = VOLUME_DISK_EXTENTS.SizeOf(1) + (i - 1) * Unsafe.SizeOf<DISK_EXTENT>();
            yield return MemoryMarshal.Read<DISK_EXTENT>(buffer.AsSpan()[start..]);
        }
    }

    internal static IEnumerable<VolumeInformation> GetVolumeInfo(bool fixedOnly) {
        foreach (var volume in GetVolumes()) {
            var volumePaths = GetVolumePaths(volume).ToList();

            if (fixedOnly && PInvoke.GetDriveType(volume) != PInvoke.DRIVE_FIXED) {
                continue;
            }

            var labelBuffer = new char[PInvoke.MAX_PATH + 1];
            var filesystemBuffer = new char[PInvoke.MAX_PATH + 1];
            if (!PInvoke.GetVolumeInformation(volume, labelBuffer, filesystemBuffer)) {
                throw new Win32Exception(nameof(PInvoke.GetVolumeInformation));
            }

            if (!PInvoke.GetDiskFreeSpaceEx(volume, out _, out var size, out var free)) {
                throw new Win32Exception(nameof(PInvoke.GetDiskFreeSpaceEx));
            }

            List<VolumeExtent> extents;
            using (var volumeHandle = File.OpenHandle(
                volume.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete)) {
                var rawExtents = GetDiskExtents(volumeHandle);
                extents = rawExtents.Select(x => new VolumeExtent() {
                    DiskNumber = x.DiskNumber,
                    StartingOffset = x.StartingOffset,
                    ExtentLength = x.ExtentLength,
                }).ToList();
            }

            yield return new() {
                ObjectPath = volume,
                MountPoints = volumePaths,
                Label = new(labelBuffer[..labelBuffer.IndexOf('\0')]),
                Filesystem = new(filesystemBuffer[..filesystemBuffer.IndexOf('\0')]),
                Size = size,
                Free = free,
                Extents = extents,
            };
        }
    }
}
