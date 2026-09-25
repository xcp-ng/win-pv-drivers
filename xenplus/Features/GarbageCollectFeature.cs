using Microsoft.Extensions.Options;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class GarbageCollectOptions {
#if DEBUG
    const bool IsGarbageCollectFeatureEnabledByDefault = true;
#else
    const bool IsGarbageCollectFeatureEnabledByDefault = false;
#endif
    public bool Enabled { get; set; } = IsGarbageCollectFeatureEnabledByDefault;
}

sealed class GarbageCollectFeature(
    IHostLifetime _hostLifetime,
    IOptionsMonitor<GarbageCollectOptions> _options,
    XenIfaceSource _xi,
    PolicyService _policy,
    ILogger<GarbageCollectFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    // https://github.com/xenserver/win-xenguestagent/blob/master/src/xenguestlib/Features.cs#L213
    const string FeatureKey = "control/garbagecollect";

    XenIfaceWatch? _watch = null;

    void Cleanup() {
        try {
            using var h = _xi.Lock();

            h.StoreRemove(FeatureKey);
        } catch (Exception ex) {
            _logger.LogDebug(ex, "{} cleanup error", nameof(GarbageCollectFeature));
        }
    }

    protected override async Task ExecuteFeatureAsync(CancellationToken stoppingToken) {
        if (_policy.DisableRemoteControl) {
            _logger.LogDebug("{} blocked by policy", nameof(GarbageCollectFeature));
            return;
        }

        if (!_options.CurrentValue.Enabled) {
            _logger.LogDebug("{} disabled by config", nameof(GarbageCollectFeature));
            return;
        }

        async void onWatch(object? sender, XenIfaceWatchEventArgs args) {
            try {
                using (var h = _xi.Lock()) {
                    if (string.IsNullOrEmpty(h.StoreTryRead(FeatureKey))) {
                        return;
                    }
                }

                _logger.LogDebug("Collecting garbage per request");

                GC.Collect();
                GC.WaitForPendingFinalizers();
            } catch (Exception ex) {
                _logger.LogDebug(ex, "{} report error", nameof(GarbageCollectFeature));
            }
        }

        bool watched = false;
        try {
            _watch = _xi.WatchAdd(FeatureKey);
            _watch.WatchTriggered += onWatch;
            watched = true;

            await Task.Delay(Timeout.Infinite, stoppingToken);
        } finally {
            if (watched) {
                _watch!.WatchTriggered -= onWatch;
            }
            _watch?.Dispose();
            _watch = null;
            Cleanup();
        }
    }
}
