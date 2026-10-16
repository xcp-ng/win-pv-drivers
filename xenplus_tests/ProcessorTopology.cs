using Windows.Win32;

namespace XenPlus;

sealed record ProcessorTopology(ushort GroupCount, uint ActiveProcessorCount, uint[] PerGroupCounts);

static class ProcessorTopologyReader {
    public static ProcessorTopology Read() {
        var groupCount = PInvokeT.GetActiveProcessorGroupCount();
        var perGroup = new uint[groupCount];
        for (ushort group = 0; group < groupCount; group++) {
            perGroup[group] = PInvokeT.GetActiveProcessorCount(group);
        }
        return new ProcessorTopology(
            groupCount,
            PInvokeT.GetActiveProcessorCount(PInvokeT.ALL_PROCESSOR_GROUPS),
            perGroup);
    }
}
