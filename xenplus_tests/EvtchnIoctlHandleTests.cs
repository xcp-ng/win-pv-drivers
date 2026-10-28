using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class EvtchnIoctlTests {
    [Fact]
    [Trait("CaseId", "E07-DUPLICATE-HANDLES")]
    [Trait("Profile", "local")]
    public void E07DuplicatedDeviceHandleRetainsItsFileObject() {
        var domain = fixture.RequireDomainId();
        using var owner = fixture.OpenDevice();
        using var duplicateDevice = XenIfaceIoctlHandle.FromSafeHandle(
            XenIfaceTestHandleDuplication.DuplicateFile(owner.SafeHandle), owner.Path);
        using var eventObject = new EventWaitHandle(false, EventResetMode.ManualReset);

        // Bound through the original handle, closed through the duplicate after the original is gone.
        using var channel = new XenIfaceTestEventChannel(
            duplicateDevice,
            XenIfaceTestEventChannel.BindUnbound(owner, domain, eventObject).Port);

        owner.Dispose();
        Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
    }

    [Fact]
    [Trait("CaseId", "E07-PAIRED-HANDLE-LIFETIME")]
    [Trait("Profile", "loopback")]
    public void E07PeerNotificationUsesDuplicatedEventAfterOriginalDeviceCloses() {
        var domain = fixture.RequireDomainId();
        using var owner = fixture.OpenDevice();
        using var duplicateDevice = XenIfaceIoctlHandle.FromSafeHandle(
            XenIfaceTestHandleDuplication.DuplicateFile(owner.SafeHandle), owner.Path);
        using var peer = owner.OpenAnother();
        using var eventA = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var eventObserver = new EventWaitHandle(false, EventResetMode.ManualReset) {
            SafeWaitHandle = XenIfaceTestHandleDuplication.DuplicateEvent(eventA.SafeWaitHandle, 0, sameAccess: true),
        };
        using var eventB = new EventWaitHandle(false, EventResetMode.ManualReset);

        // A is bound through the original handle and closed through the duplicate once the original is gone.
        using var channelA = new XenIfaceTestEventChannel(
            duplicateDevice,
            XenIfaceTestEventChannel.BindUnbound(owner, domain, eventA).Port);
        using var channelB = XenIfaceTestEventChannel.BindInterdomain(peer, domain, channelA.Port, eventB);

        // The bind IOCTLs are terminal, so their device references have been released before the original handle closes.
        eventA.Dispose();
        owner.Dispose();
        eventObserver.Reset();
        channelB.Notify();
        Assert.True(
            eventObserver.WaitOne(CompletionTimeout),
            "Peer notification was not observed through the duplicated event handle");
    }
}
