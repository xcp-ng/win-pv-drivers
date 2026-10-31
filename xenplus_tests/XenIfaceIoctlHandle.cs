using Microsoft.Win32.SafeHandles;

namespace XenPlus;

sealed class XenIfaceIoctlHandle : IDisposable {
    readonly SafeFileHandle _handle;

    XenIfaceIoctlHandle(SafeFileHandle handle, string path) {
        _handle = handle;
        Path = path;
    }

    public string Path { get; }

    public SafeFileHandle SafeHandle {
        get {
            return _handle;
        }
    }

    /// <summary>Takes ownership of the supplied safe handle.</summary>
    public static XenIfaceIoctlHandle FromSafeHandle(SafeFileHandle handle, string path) {
        return new(handle, path);
    }

    public static XenIfaceIoctlHandle Open(string devicePath) {
        return new(File.OpenHandle(
            devicePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            FileOptions.Asynchronous), devicePath);
    }

    /// <summary>Opens a second, independent file object on the same device.</summary>
    public XenIfaceIoctlHandle OpenAnother() {
        return Open(Path);
    }

    /// <summary>Starts one raw overlapped IOCTL request.</summary>
    /// <param name="writeInput">Initializes the request-owned native input buffer synchronously before submission.
    /// If null, the zeroed buffer is submitted unchanged.</param>
    /// <param name="writeOutput">Optionally seeds output storage before submission, for canary checks.</param>
    public PendingIoctl Ioctl(
        uint ioctl,
        int inputLength,
        int outputLength,
        Action<nint>? writeInput = null,
        Action<nint>? writeOutput = null) {
        return new(_handle, ioctl, inputLength, outputLength, writeInput, writeOutput);
    }

    public void Dispose() {
        _handle.Dispose();
    }
}
