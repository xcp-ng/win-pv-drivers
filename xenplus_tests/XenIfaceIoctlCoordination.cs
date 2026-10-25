namespace XenPlus;

/// <summary>Named manual-reset event barriers for the paired G06 processes.</summary>
sealed class XenIfaceIoctlCoordination : IDisposable {
    readonly DisposeStack _disposeStack = new();
    readonly EventWaitHandle _ownerReady;
    readonly EventWaitHandle _contenderReady;
    readonly EventWaitHandle _ownerRevoked;
    readonly EventWaitHandle _contenderDone;

    XenIfaceIoctlCoordination(string name, string scenario) {
        var prefix = $@"Global\XenIfaceIoctl-{name}-{scenario}";
        try {
            _ownerReady = CreateBarrier(prefix + "-owner-ready");
            _contenderReady = CreateBarrier(prefix + "-contender-ready");
            _ownerRevoked = CreateBarrier(prefix + "-owner-revoked");
            _contenderDone = CreateBarrier(prefix + "-contender-done");
        } catch {
            _disposeStack.Dispose();
            throw;
        }
    }

    /// <summary>Returns the validated coordination token, or skips the case when it is not set.</summary>
    public static string RequireToken() {
        const string environmentVariable = "XENIFACE_IOCTL_COORDINATION_NAME";
        var name = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(name)) {
            Assert.Skip($"Set -CoordinationName or {environmentVariable}; start the external partner with the same token");
        }
        var coordinationName = name!;
        if (coordinationName.Length > 64 || coordinationName.Any(character =>
                !(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-'))) {
            throw new InvalidDataException($"{environmentVariable} must contain 1-64 letters, digits, underscores, or hyphens");
        }
        return coordinationName;
    }

    public static XenIfaceIoctlCoordination Open(string scenario) {
        return new XenIfaceIoctlCoordination(RequireToken(), scenario);
    }

    public void SignalOwnerReady() {
        Signal(_ownerReady, "owner-ready");
    }

    public void SignalContenderReady() {
        Signal(_contenderReady, "contender-ready");
    }

    public void SignalOwnerRevoked() {
        Signal(_ownerRevoked, "owner-revoked");
    }

    public void SignalContenderDone() {
        Signal(_contenderDone, "contender-done");
    }

    public void WaitOwnerReady() {
        Wait(_ownerReady, "owner-ready");
    }

    public void WaitContenderReady() {
        Wait(_contenderReady, "contender-ready");
    }

    public void WaitOwnerRevoked() {
        Wait(_ownerRevoked, "owner-revoked");
    }

    public void WaitContenderDone() {
        Wait(_contenderDone, "contender-done");
    }

    EventWaitHandle CreateBarrier(string name) {
        var barrier = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        _disposeStack.Push(barrier);
        return barrier;
    }

    static void Signal(EventWaitHandle barrier, string signal) {
        barrier.Set();
        Console.WriteLine($"COORDINATION {signal}");
        Console.Out.Flush();
    }

    static void Wait(EventWaitHandle barrier, string signal) {
        Assert.True(barrier.WaitOne(XenIfaceIoctlTestSupport.CoordinationTimeout), $"Timed out waiting for G06 {signal}");
        Console.WriteLine($"COORDINATION observed {signal}");
        Console.Out.Flush();
    }

    public void Dispose() {
        _disposeStack.Dispose();
    }
}
