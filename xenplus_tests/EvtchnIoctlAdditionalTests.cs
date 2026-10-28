using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class EvtchnIoctlTests {
    [Fact]
    [Trait("CaseId", "E04-RESET-HANDSHAKE-PROGRESS")]
    [Trait("Profile", "loopback")]
    public void E04ResetEventsProveHandshakeProgressForAutoAndManualReset() {
        var domain = fixture.RequireDomainId();
        using var a = fixture.OpenDevice();
        using var b = a.OpenAnother();
        foreach (var eventMode in new[] { EventResetMode.ManualReset, EventResetMode.AutoReset }) {
            using var eventA = new EventWaitHandle(false, eventMode);
            using var eventB = new EventWaitHandle(false, eventMode);
            using var pair = new XenIfaceTestEventChannelPair(a, b, domain, eventA, eventB);

            for (var round = 0; round < 8; round++) {
                // Also drains the bind-time signals in the first round.
                eventA.Reset();
                eventB.Reset();

                // Signals can coalesce. The batch only needs to make progress at B.
                for (var notification = 0; notification < 4; notification++) {
                    pair.A.Notify();
                }
                Assert.True(
                    eventB.WaitOne(CompletionTimeout),
                    $"Endpoint B made no progress in round {round} (eventMode={eventMode})");
                eventB.Reset();

                pair.B.Notify();
                Assert.True(
                    eventA.WaitOne(CompletionTimeout),
                    $"Endpoint A received no acknowledgment in round {round} (eventMode={eventMode})");
                eventA.Reset();
            }
        }
    }
}
