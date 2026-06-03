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
    readonly Version _productVer = Version.Parse(VersionInfo.ProductVersion);

    void Report(object? sender, XenIfaceResumedEventArgs args) {
        _logger.LogTrace("{}.{}", nameof(PVVersionInfoFeature), nameof(Report));
        try {
            using var h = _xi.Lock();

            try {
                h.StoreRemove("attr/PVAddons");
            } catch {
            }

            h.StoreWrite("attr/PVAddons/MajorVersion", Utils.NormalizeVersion(_productVer.Major).ToString());
            h.StoreWrite("attr/PVAddons/MinorVersion", Utils.NormalizeVersion(_productVer.Minor).ToString());
            h.StoreWrite("attr/PVAddons/MicroVersion", Utils.NormalizeVersion(_productVer.Build).ToString());
            h.StoreWrite("attr/PVAddons/BuildVersion", Utils.NormalizeVersion(_productVer.Revision).ToString());
            h.StoreWrite("attr/PVAddons/Installed", "1");

            h.StoreWrite("data/updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(PVVersionInfoFeature));
        }
    }

    protected override async Task ExecuteFeatureAsync(CancellationToken stoppingToken) {
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
    }
}
