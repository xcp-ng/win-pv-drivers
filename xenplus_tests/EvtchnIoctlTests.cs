using System.Runtime.CompilerServices;
using Windows.Win32.System.Threading;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

[Trait("Category", "XenIfaceIoctl")]
[Collection("XenIfaceIoctl")]
public sealed partial class EvtchnIoctlTests(XenIfaceIoctlFixture fixture) : IClassFixture<XenIfaceIoctlFixture> {
    [Fact]
    [Trait("CaseId", "E01-BIND-CLOSE")]
    [Trait("Profile", "local")]
    public void E01BindUnboundAndClose() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var bind = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = notification.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bind);
        var channel = new XenIfaceTestEventChannel(handle, unchecked((uint)bind.ReadInt32()));
        using (channel) {
            Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
        }
    }

    [Fact]
    [Trait("CaseId", "E02-PAIR-NOTIFY")]
    [Trait("Profile", "loopback")]
    public void E02SelfDomainEventChannelsNotifyInBothDirections() {
        var domain = fixture.RequireDomainId();
        var (path, a) = fixture.OpenDevice();
        using var aHandle = a;
        using var b = XenIfaceIoctlHandle.Open(path);
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
        var (path, a) = fixture.OpenDevice();
        using var aHandle = a;
        using var b = XenIfaceIoctlHandle.Open(path);
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
    public unsafe void E05EventChannelRejectsMalformedBuffersAndUnknownPorts() {
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
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
        using (var nullEventBind = handle.Ioctl(
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            Unsafe.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>(),
            sizeof(uint),
            p => *(XENIFACE_EVTCHN_BIND_UNBOUND_IN*)p = new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = 0,
                Mask = 0,
                Event = 0
            })) {
            AssertRejected(nullEventBind);
        }
        using (var wrongObject = new Mutex()) {
            using var wrongTypeBind = handle.Ioctl(
                IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
                Unsafe.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>(),
                sizeof(uint),
                p => *(XENIFACE_EVTCHN_BIND_UNBOUND_IN*)p = new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                    RemoteDomain = 0,
                    Mask = 0,
                    Event = wrongObject.SafeWaitHandle.DangerousGetHandle()
                });
            AssertRejected(wrongTypeBind);
        }
        using (var restrictedEvent = XenIfaceTestHandleDuplication.DuplicateEvent(
            notification.SafeWaitHandle,
            (uint)SYNCHRONIZATION_ACCESS_RIGHTS.SYNCHRONIZATION_SYNCHRONIZE,
            sameAccess: false)) {
            using var insufficientAccessBind = handle.Ioctl(
                IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
                Unsafe.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>(),
                sizeof(uint),
                p => *(XENIFACE_EVTCHN_BIND_UNBOUND_IN*)p = new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                    RemoteDomain = 0,
                    Mask = 0,
                    Event = restrictedEvent.DangerousGetHandle()
                });
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
        var (path, owner) = fixture.OpenDevice();
        using var ownerLifetime = owner;
        using var other = XenIfaceIoctlHandle.Open(path);
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var bind = Ioctl(
            owner,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = notification.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bind);
        var port = unchecked((uint)bind.ReadInt32());
        using var channel = new XenIfaceTestEventChannel(owner, port);
        using (var wrongClose = Ioctl(
            other,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = port })) {
            Assert.False(wrongClose.IsPending, "Closing another file object's port unexpectedly returned STATUS_PENDING");
            AssertRejected(wrongClose);
        }
        using (var wrongNotify = Ioctl(
            other,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = port })) {
            AssertRejected(wrongNotify);
        }
        using (var wrongUnmask = Ioctl(
            other,
            IOCTL_XENIFACE_EVTCHN_UNMASK,
            new XENIFACE_EVTCHN_UNMASK_IN { LocalPort = port })) {
            AssertRejected(wrongUnmask);
        }
        Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
    }

    [Fact]
    [Trait("CaseId", "E09-DOUBLE-CLOSE")]
    [Trait("Profile", "local")]
    public void E09DoubleCloseReturnsFailureAndSmallOperationStillWorks() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var bind = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = notification.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bind);
        var port = unchecked((uint)bind.ReadInt32());
        using var channel = new XenIfaceTestEventChannel(handle, port);
        Assert.True(channel.Close(), "EVTCHN_CLOSE must complete synchronously and succeed");
        using (var duplicate = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = port })) {
            Assert.False(duplicate.IsPending, "A duplicate EVTCHN_CLOSE unexpectedly returned STATUS_PENDING");
            AssertRejected(duplicate);
        }
        using var smoke = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = notification.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(smoke);
        using var smokeChannel = new XenIfaceTestEventChannel(handle, unchecked((uint)smoke.ReadInt32()));
    }

    [Fact]
    [Trait("CaseId", "E10-BOUNDED-STRESS")]
    [Trait("Profile", "local")]
    public void E10BoundedBindCloseStressKeepsTheInterfaceUsable() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        for (var i = 0; i < 64; i++) {
            using var bind = Ioctl(
                handle,
                IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
                new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                    RemoteDomain = domain,
                    Mask = 0,
                    Event = notification.SafeWaitHandle.DangerousGetHandle()
                },
                sizeof(uint));
            AssertSucceeded(bind);
            using var channel = new XenIfaceTestEventChannel(handle, unchecked((uint)bind.ReadInt32()));
            Assert.True(channel.Close(), "Each stress-loop EVTCHN_CLOSE must complete synchronously and succeed");
        }
    }
}
