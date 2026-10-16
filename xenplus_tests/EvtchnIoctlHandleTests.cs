using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class EvtchnIoctlTests {
    [Fact]
    [Trait("CaseId", "E07-DUPLICATE-HANDLES")]
    [Trait("Profile", "local")]
    public void E07DuplicatedDeviceHandleRetainsItsFileObject() {
        var domain = fixture.RequireDomainId();
        var (_, owner) = fixture.OpenDevice();
        using var ownerLifetime = owner;
        using var duplicateDevice = XenIfaceIoctlHandle.FromSafeHandle(
            XenIfaceTestHandleDuplication.DuplicateFile(owner.SafeHandle));
        using var eventObject = new EventWaitHandle(false, EventResetMode.ManualReset);

        using var bind = Ioctl(
            owner,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = eventObject.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bind);
        using var channel = new XenIfaceTestEventChannel(duplicateDevice, unchecked((uint)bind.ReadInt32()));

        // The bind has completed before the original device handle closes.
        owner.Dispose();
        Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
    }

    [Fact]
    [Trait("CaseId", "E07-PAIRED-HANDLE-LIFETIME")]
    [Trait("Profile", "loopback")]
    public void E07PeerNotificationUsesDuplicatedEventAfterOriginalDeviceCloses() {
        var domain = fixture.RequireDomainId();
        var (path, owner) = fixture.OpenDevice();
        using var ownerLifetime = owner;
        using var duplicateDevice = XenIfaceIoctlHandle.FromSafeHandle(
            XenIfaceTestHandleDuplication.DuplicateFile(owner.SafeHandle));
        using var peer = XenIfaceIoctlHandle.Open(path);
        using var eventA = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var eventObserver = new EventWaitHandle(false, EventResetMode.ManualReset) {
            SafeWaitHandle = XenIfaceTestHandleDuplication.DuplicateEvent(eventA.SafeWaitHandle, 0, sameAccess: true),
        };
        using var eventB = new EventWaitHandle(false, EventResetMode.ManualReset);

        using var bindA = Ioctl(
            owner,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = eventA.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bindA);
        using var channelA = new XenIfaceTestEventChannel(duplicateDevice, unchecked((uint)bindA.ReadInt32()));
        using var bindB = Ioctl(
            peer,
            IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN,
            new XENIFACE_EVTCHN_BIND_INTERDOMAIN_IN {
                RemoteDomain = domain,
                RemotePort = channelA.Port,
                Mask = 0,
                Event = eventB.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bindB);
        using var channelB = new XenIfaceTestEventChannel(peer, unchecked((uint)bindB.ReadInt32()));

        // The bind IOCTLs are terminal, so their device references have been released before the original handle closes.
        eventA.Dispose();
        owner.Dispose();
        eventObserver.Reset();
        using (var notifyPeer = Ioctl(
            peer,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = channelB.Port })) {
            AssertSucceeded(notifyPeer);
        }
        Assert.True(
            eventObserver.WaitOne(CompletionTimeout),
            "Peer notification was not observed through the duplicated event handle");

    }
}
