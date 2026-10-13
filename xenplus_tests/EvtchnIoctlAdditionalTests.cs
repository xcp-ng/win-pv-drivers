using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class EvtchnIoctlTests {
    [Fact]
    [Trait("CaseId", "E04-RESET-HANDSHAKE-PROGRESS")]
    [Trait("Profile", "loopback")]
    public void E04ResetEventsProveHandshakeProgressForAutoAndManualReset() {
        var domain = fixture.RequireDomainId();
        var (path, a) = fixture.OpenDevice();
        using var aLifetime = a;
        using var b = XenIfaceIoctlHandle.Open(path);
        foreach (var eventMode in new[] { EventResetMode.ManualReset, EventResetMode.AutoReset }) {
            using var eventA = new EventWaitHandle(false, eventMode);
            using var eventB = new EventWaitHandle(false, eventMode);
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
            using var channelA = new XenIfaceTestEventChannel(a, unchecked((uint)bindA.ReadInt32()));
            using var bindB = Ioctl(
                b,
                IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN,
                new XENIFACE_EVTCHN_BIND_INTERDOMAIN_IN {
                    RemoteDomain = domain,
                    RemotePort = channelA.Port,
                    Mask = 0,
                    Event = eventB.SafeWaitHandle.DangerousGetHandle()
                },
                sizeof(uint));
            AssertSucceeded(bindB);
            using var channelB = new XenIfaceTestEventChannel(b, unchecked((uint)bindB.ReadInt32()));

            // Drain bind-time signals before the first measured handshake.
            eventA.Reset();
            eventB.Reset();

            for (var round = 0; round < 8; round++) {
                eventA.Reset();
                eventB.Reset();

                // Signals can coalesce. The batch only needs to make progress at B.
                for (var notification = 0; notification < 4; notification++) {
                    using var notify = Ioctl(
                        a,
                        IOCTL_XENIFACE_EVTCHN_NOTIFY,
                        new XENIFACE_EVTCHN_NOTIFY_IN {
                            LocalPort = channelA.Port
                        });
                    AssertSucceeded(notify);
                }
                Assert.True(
                    eventB.WaitOne(CompletionTimeout),
                    $"Endpoint B made no progress in round {round} (eventMode={eventMode})");
                eventB.Reset();

                using (var acknowledge = Ioctl(
                    b,
                    IOCTL_XENIFACE_EVTCHN_NOTIFY,
                    new XENIFACE_EVTCHN_NOTIFY_IN {
                        LocalPort = channelB.Port
                    })) {
                    AssertSucceeded(acknowledge);
                }
                Assert.True(
                    eventA.WaitOne(CompletionTimeout),
                    $"Endpoint A received no acknowledgment in round {round} (eventMode={eventMode})");
                eventA.Reset();
            }
        }
    }
}
