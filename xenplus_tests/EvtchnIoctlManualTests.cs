using System.Diagnostics;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class EvtchnIoctlTests {
    /// <summary>
    /// Needs fewer vCPUs online than the VM maximum.
    /// Run <c>xenplus_tests.exe -explicit only -trait CaseId=E11-CPU-HOTPLUG</c>,
    /// wait for <c>E11 ready active=N</c>, then within 10 min add a vCPU from the host
    /// (e.g. <c>xe vm-vcpu-hotplug uuid=UUID new-vcpus=N+1</c>). Added vCPUs stay online until restart.
    /// </summary>
    [Fact(Explicit = true)]
    [Trait("CaseId", "E11-CPU-HOTPLUG")]
    [Trait("Profile", "manual")]
    [Trait("ExternalCoordination", "true")]
    public async Task E11EventChannelPairContinuesAcrossExternallyManagedCpuAddition() {
        var domain = fixture.RequireDomainId();
        using var a = fixture.OpenDevice();
        using var b = a.OpenAnother();
        using var eventA = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var eventB = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var pair = new XenIfaceTestEventChannelPair(a, b, domain, eventA, eventB);

        var baseline = ProcessorTopologyReader.Read();
        pair.Handshake();
        WriteE11Signal("ready", baseline);
        Console.WriteLine($"E11 waiting for external CPU addition; active={baseline.ActiveProcessorCount}, groups={baseline.GroupCount}");

        var added = await WaitForCpuAdditionAsync(pair, baseline.ActiveProcessorCount);
        WriteE11Signal("added observed", added);
        pair.Handshake();
        Assert.True(pair.B.Close(), "EVTCHN_CLOSE for channel B must complete synchronously and succeed");
        Assert.True(pair.A.Close(), "EVTCHN_CLOSE for channel A must complete synchronously and succeed");

        // A fresh pair must still work after the topology change.
        using var freshEventA = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var freshEventB = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var freshPair = new XenIfaceTestEventChannelPair(a, b, domain, freshEventA, freshEventB);
        freshPair.Handshake();
    }

    static void WriteE11Signal(string signal, ProcessorTopology topology) {
        Console.WriteLine($"E11 {signal} active={topology.ActiveProcessorCount}, groups={topology.GroupCount}, perGroup={string.Join(',', topology.PerGroupCounts)}");
        Console.Out.Flush();
    }

    static async Task<ProcessorTopology> WaitForCpuAdditionAsync(XenIfaceTestEventChannelPair pair, uint baselineActiveCount) {
        var deadline = Stopwatch.GetTimestamp() + (long)(CoordinationTimeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline) {
            pair.Handshake();
            var current = ProcessorTopologyReader.Read();
            if (current.ActiveProcessorCount > baselineActiveCount) {
                return current;
            }
            await Task.Delay(CpuAdditionPollInterval, TestContext.Current.CancellationToken);
        }
        Assert.Fail("Windows did not report an increased active processor count after external CPU addition");
        throw new InvalidOperationException("Unreachable after Assert.Fail");
    }
}
