using Microsoft.Extensions.Options;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class PVVersionInfoOptions {
    public bool Enabled { get; set; } = true;
}

sealed class PVVersionInfoFeature(
    IHostLifetime _hostLifetime,
    IOptionsMonitor<PVVersionInfoOptions> _options,
    XenIfaceSource _xi,
    ILogger<PVVersionInfoFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    const uint _assert_ProductVersion = VersionInfo.ProductVersion == "" ? -1 : 0;
    readonly Version _productVer = Version.Parse(VersionInfo.ProductVersion);

    void Report(object? sender, XenIfaceResumedEventArgs args) {
        _logger.LogTrace("{}.{}", nameof(PVVersionInfoFeature), nameof(Report));
        try {
            using var h = _xi.Lock();

            try {
                h.StoreRemove("attr/PVAddons");
            } catch {
            }

            h.StoreWrite("attr/PVAddons/MajorVersion", ServerUtils.NormalizeVersion(_productVer.Major));
            h.StoreWrite("attr/PVAddons/MinorVersion", ServerUtils.NormalizeVersion(_productVer.Minor));
            h.StoreWrite("attr/PVAddons/MicroVersion", ServerUtils.NormalizeVersion(_productVer.Build));
            h.StoreWrite("attr/PVAddons/BuildVersion", ServerUtils.NormalizeVersion(_productVer.Revision));
            h.StoreWrite("attr/PVAddons/Installed", "1");
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(PVVersionInfoFeature));
        }
    }

    void Cleanup() {
        try {
            using var h = _xi.Lock();

            h.StoreRemove("attr/PVAddons");
        } catch (Exception ex) {
            _logger.LogDebug(ex, "{} cleanup error", nameof(PVVersionInfoFeature));
        }
    }

    protected override async Task ExecuteFeatureAsync(CancellationToken stoppingToken) {
        try {
            if (!_options.CurrentValue.Enabled) {
                return;
            }
            _xi.Resumed += Report;
            try {
                Report(null, new());
                await Task.Delay(Timeout.Infinite, stoppingToken);
            } finally {
                _xi.Resumed -= Report;
            }
        } finally {
            Cleanup();
        }
    }
}
