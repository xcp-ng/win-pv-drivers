using System.Diagnostics;
using XenPlus.XenIface;

namespace XenPlus;

static class XenIfaceSourceTestSupport {
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DeviceRemovalBlockedTimeout = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan StatePollInterval = TimeSpan.FromMilliseconds(10);

    public static async Task WaitForWatchReadyAsync(XenIfaceWatch watch, CancellationToken cancellationToken) {
        var deadline = Stopwatch.GetTimestamp() + (long)(EventTimeout.TotalSeconds * Stopwatch.Frequency);
        while (!watch.Ready) {
            if (Stopwatch.GetTimestamp() >= deadline) {
                throw new TimeoutException("The watch did not consume its initial notification");
            }
            await Task.Delay(StatePollInterval, cancellationToken);
        }
    }

    public static async Task<XenIfaceDevice> WaitForActiveDeviceAsync(
        XenIfaceSource source,
        CancellationToken cancellationToken) {

        var deadline = Stopwatch.GetTimestamp() + (long)(EventTimeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline) {
            lock (source.SyncRoot) {
                if (source.Active is XenIfaceDevice active) {
                    return active;
                }
            }
            await Task.Delay(StatePollInterval, cancellationToken);
        }
        throw new TimeoutException("The test source did not open a XenIface device");
    }
}
