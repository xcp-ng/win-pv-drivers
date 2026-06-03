using System.Net;
using Microsoft.Extensions.Options;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class OSInfoOptions {
    public bool Enabled { get; set; } = true;
}

sealed class OSInfoFeature(
    IHostLifetime _hostLifetime,
    IOptionsMonitor<OSInfoOptions> _options,
    XenIfaceSource _xi,
    OSInfoService _osInfoService,
    ILogger<OSInfoFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    readonly Lazy<OSInfo> _osInfo = new(_osInfoService.Query);

    void Report(object? sender, XenIfaceResumedEventArgs args) {
        _logger.LogTrace("{}.{}", nameof(OSInfoFeature), nameof(Report));
        try {
            using var h = _xi.Lock();

            try {
                h.StoreRemove("attr/os");
            } catch {
            }

            h.StoreWrite("attr/os/class", "Windows NT");

            // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/5.0/environment-osversion-returns-correct-version
            h.StoreWrite("attr/os/major", Utils.NormalizeVersion(Environment.OSVersion.Version.Major).ToString());
            h.StoreWrite("attr/os/minor", Utils.NormalizeVersion(Environment.OSVersion.Version.Minor).ToString());
            h.StoreWrite("attr/os/build", Utils.NormalizeVersion(Environment.OSVersion.Version.Build).ToString());
            h.StoreWrite("attr/os/platform", Environment.OSVersion.Platform.ToString());

            h.StoreWrite("data/os_distro", "Windows");

            h.StoreWrite("data/os_majorver", Utils.NormalizeVersion(Environment.OSVersion.Version.Major).ToString());
            h.StoreWrite("data/os_minorver", Utils.NormalizeVersion(Environment.OSVersion.Version.Minor).ToString());
            h.StoreWrite("data/os_buildver", Utils.NormalizeVersion(Environment.OSVersion.Version.Build).ToString());

            h.StoreWrite("data/os_uname", Environment.OSVersion.Version.ToString());

            h.StoreWrite("data/os_name", _osInfo.Value.Caption);
            h.StoreWrite("data/host_name", Environment.MachineName);
            h.StoreWrite("data/host_name_dns", Dns.GetHostName());
            if (_osInfo.Value.IsDomainJoined) {
                h.StoreWrite("data/domain", _osInfo.Value.DomainNameDns ?? _osInfo.Value.DomainNameFlat);
            } else {
                try {
                    h.StoreRemove("data/domain");
                } catch {
                }
            }

            h.StoreWrite("data/updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(OSInfoFeature));
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
