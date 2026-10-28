using System.Runtime.CompilerServices;
using Windows.Win32.System.Threading;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

[Trait("Category", "XenIfaceIoctl")]
[Collection("XenIfaceIoctl")]
public sealed partial class EvtchnIoctlTests(XenIfaceIoctlFixture fixture) : IClassFixture<XenIfaceIoctlFixture> {
    static PendingIoctl BeginBindUnbound(XenIfaceIoctlHandle handle, nint evt) {
        return Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = 0,
                Mask = 0,
                Event = evt
            },
            sizeof(uint));
    }

    [Fact]
    [Trait("CaseId", "E01-BIND-CLOSE")]
    [Trait("Profile", "local")]
    public void E01BindUnboundAndClose() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var channel = XenIfaceTestEventChannel.BindUnbound(handle, domain, notification);
        Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
    }

    [Fact]
    [Trait("CaseId", "E02-PAIR-NOTIFY")]
    [Trait("Profile", "loopback")]
    public void E02SelfDomainEventChannelsNotifyInBothDirections() {
        var domain = fixture.RequireDomainId();
        using var a = fixture.OpenDevice();
        using var b = a.OpenAnother();
        using var eventA = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var eventB = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var pair = new XenIfaceTestEventChannelPair(a, b, domain, eventA, eventB);

        pair.Handshake();
    }

    [Fact]
    [Trait("CaseId", "E03-MASK-UNMASK")]
    [Trait("Profile", "loopback")]
    public void E03MaskedNotificationIsDeliveredAfterUnmask() {
        var domain = fixture.RequireDomainId();
        using var a = fixture.OpenDevice();
        using var b = a.OpenAnother();
        using var eventA = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var eventB = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var pair = new XenIfaceTestEventChannelPair(a, b, domain, eventA, eventB, maskB: 1);
        eventA.Reset();
        eventB.Reset();

        pair.A.Notify();
        Assert.False(eventB.WaitOne(ObservationTimeout), "A masked endpoint delivered before unmask");
        pair.B.Unmask();
        Assert.True(eventB.WaitOne(CompletionTimeout), "Pending notification was not delivered after unmask");
    }

    [Fact]
    [Trait("CaseId", "E05-BUFFER-ERRORS")]
    [Trait("Profile", "local")]
    public void E05EventChannelRejectsMalformedBuffersAndUnknownPorts() {
        using var handle = fixture.OpenDevice();
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using (var shortBind = handle.Ioctl(
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            Unsafe.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>() - 1,
            sizeof(uint))) {
            AssertRejected(shortBind);
        }
        using (var longBind = handle.Ioctl(
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            Unsafe.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>() + 1,
            sizeof(uint))) {
            AssertRejected(longBind);
        }
        using (var nullEventBind = BeginBindUnbound(handle, 0)) {
            AssertRejected(nullEventBind);
        }
        using (var wrongObject = new Mutex()) {
            using var wrongTypeBind = BeginBindUnbound(handle, wrongObject.SafeWaitHandle.DangerousGetHandle());
            AssertRejected(wrongTypeBind);
        }
        using (var restrictedEvent = XenIfaceTestHandleDuplication.DuplicateEvent(
            notification.SafeWaitHandle,
            (uint)SYNCHRONIZATION_ACCESS_RIGHTS.SYNCHRONIZATION_SYNCHRONIZE,
            sameAccess: false)) {
            using var insufficientAccessBind = BeginBindUnbound(handle, restrictedEvent.DangerousGetHandle());
            AssertRejected(insufficientAccessBind);
        }
        using (var unknownClose = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = uint.MaxValue })) {
            Assert.False(unknownClose.IsPending, "Closing an unknown port unexpectedly returned STATUS_PENDING");
            AssertRejected(unknownClose);
        }
        using (var unknownNotify = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = uint.MaxValue })) {
            AssertRejected(unknownNotify);
        }
        using (var unknownUnmask = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_UNMASK,
            new XENIFACE_EVTCHN_UNMASK_IN { LocalPort = uint.MaxValue })) {
            AssertRejected(unknownUnmask);
        }
    }

    [Fact]
    [Trait("CaseId", "E06-FILEOBJECT-OWNERSHIP")]
    [Trait("Profile", "local")]
    public void E06IndependentDeviceOpensCannotCloseEachOthersChannels() {
        var domain = fixture.RequireDomainId();
        using var owner = fixture.OpenDevice();
        using var other = owner.OpenAnother();
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var channel = XenIfaceTestEventChannel.BindUnbound(owner, domain, notification);
        using (var wrongClose = Ioctl(
            other,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = channel.Port })) {
            Assert.False(wrongClose.IsPending, "Closing another file object's port unexpectedly returned STATUS_PENDING");
            AssertRejected(wrongClose);
        }
        using (var wrongNotify = Ioctl(
            other,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = channel.Port })) {
            AssertRejected(wrongNotify);
        }
        using (var wrongUnmask = Ioctl(
            other,
            IOCTL_XENIFACE_EVTCHN_UNMASK,
            new XENIFACE_EVTCHN_UNMASK_IN { LocalPort = channel.Port })) {
            AssertRejected(wrongUnmask);
        }
        Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
    }

    [Fact]
    [Trait("CaseId", "E09-DOUBLE-CLOSE")]
    [Trait("Profile", "local")]
    public void E09DoubleCloseReturnsFailureAndSmallOperationStillWorks() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var channel = XenIfaceTestEventChannel.BindUnbound(handle, domain, notification);
        Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
        using (var duplicate = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = channel.Port })) {
            Assert.False(duplicate.IsPending, "A duplicate EVTCHN_CLOSE unexpectedly returned STATUS_PENDING");
            AssertRejected(duplicate);
        }
        using var smokeChannel = XenIfaceTestEventChannel.BindUnbound(handle, domain, notification);
    }

    [Fact]
    [Trait("CaseId", "E10-BOUNDED-STRESS")]
    [Trait("Profile", "local")]
    public void E10BoundedBindCloseStressKeepsTheInterfaceUsable() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        for (var i = 0; i < 64; i++) {
            using var channel = XenIfaceTestEventChannel.BindUnbound(handle, domain, notification);
            Assert.True(channel.Close(), "Each stress-loop EVTCHN_CLOSE must complete synchronously and succeed");
        }
    }
}
