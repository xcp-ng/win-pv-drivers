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

/// <summary>
/// Completes with the source's active device when <see cref="XenIfaceSource.Resumed"/> fires after the source has
/// rebound to a device other than <c>original</c>. Ignores events until armed.
/// </summary>
sealed class ResumedObserver : IDisposable {
    readonly XenIfaceSource _source;
    readonly XenIfaceDevice? _original;
    readonly TaskCompletionSource<XenIfaceDevice> _resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool _armed;

    public ResumedObserver(XenIfaceSource source, XenIfaceDevice? original = null, bool armed = true) {
        _source = source;
        _original = original;
        _armed = armed;
        source.Resumed += OnResumed;
    }

    public Task<XenIfaceDevice> Resumed {
        get {
            return _resumed.Task;
        }
    }

    public void Arm() {
        Volatile.Write(ref _armed, true);
    }

    void OnResumed(object? sender, XenIfaceResumedEventArgs args) {
        if (!Volatile.Read(ref _armed)) {
            return;
        }
        lock (_source.SyncRoot) {
            if (_source.Active is XenIfaceDevice active && !ReferenceEquals(active, _original)) {
                _resumed.TrySetResult(active);
            }
        }
    }

    public void Dispose() {
        _source.Resumed -= OnResumed;
    }
}

/// <summary>Completes on the first trigger after <see cref="Arm"/>, ignoring registration and rearm signals.</summary>
sealed class WatchObserver : IDisposable {
    readonly XenIfaceWatch _watch;
    readonly TaskCompletionSource _triggered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool _armed;

    public WatchObserver(XenIfaceWatch watch) {
        _watch = watch;
        watch.WatchTriggered += OnWatchTriggered;
    }

    public Task Triggered {
        get {
            return _triggered.Task;
        }
    }

    public void Arm() {
        Volatile.Write(ref _armed, true);
    }

    void OnWatchTriggered(object? sender, XenIfaceWatchEventArgs args) {
        if (Volatile.Read(ref _armed)) {
            _triggered.TrySetResult();
        }
    }

    public void Dispose() {
        _watch.WatchTriggered -= OnWatchTriggered;
    }
}
