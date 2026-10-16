using System.Diagnostics;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class EvtchnIoctlTests {
    [Fact(Explicit = true)]
    [Trait("CaseId", "E11-CPU-HOTPLUG")]
    [Trait("Profile", "manual")]
    [Trait("ExternalCoordination", "true")]
    public async Task E11EventChannelPairContinuesAcrossExternallyManagedCpuAddition() {
        var domain = fixture.RequireDomainId();
        var (path, a) = fixture.OpenDevice();
        using var aLifetime = a;
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

        eventA.Reset();
        eventB.Reset();
        var baseline = ProcessorTopologyReader.Read();
        ExchangeEventChannelHandshake(a, b, eventA, eventB, channelA.Port, channelB.Port);
        WriteE11Signal("ready", baseline);
        Console.WriteLine($"E11 waiting for external CPU addition; active={baseline.ActiveProcessorCount}, groups={baseline.GroupCount}");

        var added = await WaitForCpuAdditionAsync(
            a,
            b,
            eventA,
            eventB,
            channelA.Port,
            channelB.Port,
            baseline.ActiveProcessorCount);
        WriteE11Signal("added observed", added);
        ExchangeEventChannelHandshake(a, b, eventA, eventB, channelA.Port, channelB.Port);
        Assert.True(channelB.Close(), "EVTCHN_CLOSE for channel B must complete synchronously and succeed");
        Assert.True(channelA.Close(), "EVTCHN_CLOSE for channel A must complete synchronously and succeed");
        AssertFreshEventChannelPair(a, b, domain);
    }

    static void WriteE11Signal(string signal, ProcessorTopology topology) {
        Console.WriteLine($"E11 {signal} active={topology.ActiveProcessorCount}, groups={topology.GroupCount}, perGroup={string.Join(',', topology.PerGroupCounts)}");
        Console.Out.Flush();
    }

    static async Task<ProcessorTopology> WaitForCpuAdditionAsync(
        XenIfaceIoctlHandle a,
        XenIfaceIoctlHandle b,
        EventWaitHandle eventA,
        EventWaitHandle eventB,
        uint portA,
        uint portB,
        uint baselineActiveCount) {
        var deadline = Stopwatch.GetTimestamp() + (long)(CoordinationTimeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline) {
            ExchangeEventChannelHandshake(a, b, eventA, eventB, portA, portB);
            var current = ProcessorTopologyReader.Read();
            if (current.ActiveProcessorCount > baselineActiveCount) {
                return current;
            }
            await Task.Delay(CpuAdditionPollInterval, TestContext.Current.CancellationToken);
        }
        Assert.Fail("Windows did not report an increased active processor count after external CPU addition");
        throw new InvalidOperationException("Unreachable after Assert.Fail");
    }

    static void ExchangeEventChannelHandshake(
        XenIfaceIoctlHandle a,
        XenIfaceIoctlHandle b,
        EventWaitHandle eventA,
        EventWaitHandle eventB,
        uint portA,
        uint portB) {
        eventA.Reset();
        eventB.Reset();
        using (var notifyA = Ioctl(
            a,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = portA })) {
            AssertSucceeded(notifyA);
        }
        Assert.True(eventB.WaitOne(CompletionTimeout), "Endpoint B did not observe event-channel progress during CPU hotplug");
        eventB.Reset();
        using (var notifyB = Ioctl(
            b,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = portB })) {
            AssertSucceeded(notifyB);
        }
        Assert.True(eventA.WaitOne(CompletionTimeout), "Endpoint A did not observe event-channel progress during CPU hotplug");
    }

    static void AssertFreshEventChannelPair(XenIfaceIoctlHandle a, XenIfaceIoctlHandle b, ushort domain) {
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
        eventA.Reset();
        eventB.Reset();
        ExchangeEventChannelHandshake(a, b, eventA, eventB, channelA.Port, channelB.Port);
    }
}
