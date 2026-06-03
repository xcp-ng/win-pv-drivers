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
    void Report(object? sender, XenIfaceResumedEventArgs args) {
        _logger.LogTrace("{}.{}", nameof(OSInfoFeature), nameof(Report));
        try {
            using var h = _xi.Lock();
            // requery OS info every report to ensure freshness
            var osInfo = _osInfoService.Query();

            // Schema partially inherited from XenServer:
            // https://github.com/xenserver/win-xenguestagent/blob/master/src/xenguestlib/PVInstallation.cs

            try {
                h.StoreRemove("attr/os");
            } catch {
            }

            h.StoreWrite("attr/os/class", "Windows NT");

            // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/5.0/environment-osversion-returns-correct-version
            h.StoreWrite("attr/os/major", Utils.NormalizeVersion(Environment.OSVersion.Version.Major));
            h.StoreWrite("attr/os/minor", Utils.NormalizeVersion(Environment.OSVersion.Version.Minor));
            h.StoreWrite("attr/os/build", Utils.NormalizeVersion(Environment.OSVersion.Version.Build));
            h.StoreWrite("attr/os/platform", Environment.OSVersion.Platform.ToString());

            h.StoreWrite("data/os_distro", "Windows");

            h.StoreWrite("data/os_majorver", Utils.NormalizeVersion(Environment.OSVersion.Version.Major));
            h.StoreWrite("data/os_minorver", Utils.NormalizeVersion(Environment.OSVersion.Version.Minor));
            h.StoreWrite("data/os_buildver", Utils.NormalizeVersion(Environment.OSVersion.Version.Build));

            h.StoreWrite("data/os_uname", Environment.OSVersion.Version.ToString());

            h.StoreWrite("data/os_name", osInfo.Caption);
            h.StoreWrite("data/host_name", Environment.MachineName);
            h.StoreWrite("data/host_name_dns", Dns.GetHostName());
            if (osInfo.IsDomainJoined) {
                h.StoreWrite("data/domain", osInfo.DomainNameDns ?? osInfo.DomainNameFlat);
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
