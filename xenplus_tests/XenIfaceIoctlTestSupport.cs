using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;

namespace XenPlus;

[CollectionDefinition("XenIfaceIoctl", DisableParallelization = true)]
public sealed class XenIfaceIoctlCollection;

static class XenIfaceIoctlTestSupport {
    static readonly Lazy<SYSTEM_INFO> SystemInfo = new(ReadSystemInfo);

    static unsafe SYSTEM_INFO ReadSystemInfo() {
        var systemInfo = default(SYSTEM_INFO);
        PInvokeT.GetSystemInfo(&systemInfo);
        return systemInfo;
    }

    public static int PageSize => (int)SystemInfo.Value.dwPageSize;
    public static int AllocationGranularity => (int)SystemInfo.Value.dwAllocationGranularity;

    public static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ObservationTimeout = TimeSpan.FromMilliseconds(250);

    public static readonly TimeSpan CpuAdditionPollInterval = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan CoordinationTimeout = TimeSpan.FromMinutes(10);

    public static PendingIoctl Ioctl<T>(
        XenIfaceIoctlHandle handle,
        uint code,
        T input,
        int outputLength = 0,
        Action<nint>? writeOutput = null) where T : struct {
        var inputLength = Marshal.SizeOf<T>();
        return handle.Ioctl(code, inputLength, outputLength, p => Marshal.StructureToPtr(input, p, false), writeOutput);
    }

    public static void AssertSucceeded(PendingIoctl call) {
        if (call.IsPending) {
            Assert.True(call.WaitTerminal(CompletionTimeout), "The IOCTL did not complete before the timeout");
        }
        Assert.True(call.TerminalSuccess, $"IOCTL failed; immediate error={call.ImmediateError}, terminal error={call.TerminalError}");
    }

    public static void AssertPending(PendingIoctl call) {
        Assert.True(call.IsPending, $"Expected STATUS_PENDING; immediateSuccess={call.ImmediateSuccess}, error={call.ImmediateError}");
    }

    public static void AssertRejected(PendingIoctl call) {
        Assert.False(call.ImmediateSuccess, "The rejected request completed successfully");
        if (call.IsPending) {
            Assert.True(call.WaitTerminal(CompletionTimeout), "Rejected request did not reach a terminal result");
            Assert.False(call.TerminalSuccess, "The rejected request completed successfully");
            Assert.NotEqual(0, call.TerminalError);
            return;
        }
        Assert.NotEqual(0, call.ImmediateError);
    }

}
