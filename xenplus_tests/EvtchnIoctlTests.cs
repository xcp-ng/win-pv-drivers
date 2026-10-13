using System.Runtime.InteropServices;
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
            channel.Close();
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

        using var bindA = Ioctl(
            a,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = eventA.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bindA);
        var portA = unchecked((uint)bindA.ReadInt32());
        using var channelA = new XenIfaceTestEventChannel(a, portA);
        using var bindB = Ioctl(
            b,
            IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN,
            new XENIFACE_EVTCHN_BIND_INTERDOMAIN_IN {
                RemoteDomain = domain,
                RemotePort = portA,
                Mask = 0,
                Event = eventB.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bindB);
        var portB = unchecked((uint)bindB.ReadInt32());
        using var channelB = new XenIfaceTestEventChannel(b, portB);
        eventA.Reset();
        eventB.Reset();

        using (var notifyA = Ioctl(
            a,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = portA })) {
            AssertSucceeded(notifyA);
        }
        Assert.True(eventB.WaitOne(CompletionTimeout), "Endpoint B did not observe A's notify");
        eventB.Reset();
        using (var notifyB = Ioctl(
            b,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = portB })) {
            AssertSucceeded(notifyB);
        }
        Assert.True(eventA.WaitOne(CompletionTimeout), "Endpoint A did not observe B's notify");

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
        using var bindA = Ioctl(
            a,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = eventA.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bindA);
        var portA = unchecked((uint)bindA.ReadInt32());
        using var channelA = new XenIfaceTestEventChannel(a, portA);
        using var bindB = Ioctl(
            b,
            IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN,
            new XENIFACE_EVTCHN_BIND_INTERDOMAIN_IN {
                RemoteDomain = domain,
                RemotePort = portA,
                Mask = 1,
                Event = eventB.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bindB);
        var portB = unchecked((uint)bindB.ReadInt32());
        using var channelB = new XenIfaceTestEventChannel(b, portB);
        eventA.Reset();
        eventB.Reset();
        using (var notify = Ioctl(
            a,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = portA })) {
            AssertSucceeded(notify);
        }
        Assert.False(eventB.WaitOne(ObservationTimeout), "A masked endpoint delivered before unmask");
        using (var unmask = Ioctl(
            b,
            IOCTL_XENIFACE_EVTCHN_UNMASK,
            new XENIFACE_EVTCHN_UNMASK_IN { LocalPort = portB })) {
            AssertSucceeded(unmask);
        }
        Assert.True(eventB.WaitOne(CompletionTimeout), "Pending notification was not delivered after unmask");
    }

    [Fact]
    [Trait("CaseId", "E05-BUFFER-ERRORS")]
    [Trait("Profile", "local")]
    public void E05EventChannelRejectsMalformedBuffersAndUnknownPorts() {
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using (var shortBind = handle.Ioctl(
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            Marshal.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>() - 1,
            sizeof(uint))) {
            AssertRejected(shortBind);
        }
        using (var longBind = handle.Ioctl(
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            Marshal.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>() + 1,
            sizeof(uint))) {
            AssertRejected(longBind);
        }
        using (var nullEventBind = handle.Ioctl(
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            Marshal.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>(),
            sizeof(uint),
            p => Marshal.StructureToPtr(new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = 0,
                Mask = 0,
                Event = 0
            },
            p,
            false))) {
            AssertRejected(nullEventBind);
        }
        using (var wrongObject = new Mutex()) {
            using var wrongTypeBind = handle.Ioctl(
                IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
                Marshal.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>(),
                sizeof(uint),
                p => Marshal.StructureToPtr(new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                    RemoteDomain = 0,
                    Mask = 0,
                    Event = wrongObject.SafeWaitHandle.DangerousGetHandle()
                },
                p,
                false));
            AssertRejected(wrongTypeBind);
        }
        using (var restrictedEvent = XenIfaceTestHandleDuplication.DuplicateEvent(
            notification.SafeWaitHandle,
            (uint)SYNCHRONIZATION_ACCESS_RIGHTS.SYNCHRONIZATION_SYNCHRONIZE,
            sameAccess: false)) {
            using var insufficientAccessBind = handle.Ioctl(
                IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
                Marshal.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>(),
                sizeof(uint),
                p => Marshal.StructureToPtr(new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                    RemoteDomain = 0,
                    Mask = 0,
                    Event = restrictedEvent.DangerousGetHandle()
                },
                p,
                false));
            AssertRejected(insufficientAccessBind);
        }
        using (var unknownClose = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = uint.MaxValue })) {
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
        channel.Close();
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
        channel.Close();
        using (var duplicate = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = port })) {
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
        }
    }
}
