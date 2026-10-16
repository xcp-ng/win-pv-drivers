using Microsoft.Win32.SafeHandles;

namespace XenPlus;

sealed class XenIfaceIoctlHandle : IDisposable {
    readonly SafeFileHandle _handle;

    XenIfaceIoctlHandle(SafeFileHandle handle) {
        _handle = handle;
    }

    public SafeFileHandle SafeHandle {
        get {
            return _handle;
        }
    }

    /// <summary>Takes ownership of the supplied safe handle.</summary>
    public static XenIfaceIoctlHandle FromSafeHandle(SafeFileHandle handle) {
        return new(handle);
    }

    public static XenIfaceIoctlHandle Open(string devicePath) {
        return new(File.OpenHandle(
            devicePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            FileOptions.Asynchronous));
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
