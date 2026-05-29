using Microsoft.Extensions.Options;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class PVVersionInfoOptions {
    public bool Enabled { get; set; } = true;
}

sealed class PVVersionInfoFeature(
    IOptionsSnapshot<PVVersionInfoOptions> _options,
    XenIfaceSource _xi,
    ILogger<PVVersionInfoFeature> _logger) : BackgroundService {
    readonly Version _productVer = Version.Parse(VersionInfo.ProductVersion);

    void OnResume(object? sender, XenIfaceResumedEventArgs args) {
        try {
            using var h = _xi.Lock();

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!_options.Value.Enabled) {
            return;
        }
        _logger.LogTrace("Starting {}", nameof(PVVersionInfoFeature));
        _xi.Resumed += OnResume;
        try {
            OnResume(null, new());
            await Task.Delay(Timeout.Infinite, stoppingToken);
        } finally {
            _xi.Resumed -= OnResume;
        }
    }
}
