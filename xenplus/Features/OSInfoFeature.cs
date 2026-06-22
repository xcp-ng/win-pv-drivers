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
            h.StoreWrite("attr/os/major", ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Major));
            h.StoreWrite("attr/os/minor", ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Minor));
            h.StoreWrite("attr/os/build", ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Build));
            h.StoreWrite("attr/os/platform", Environment.OSVersion.Platform.ToString());
            h.StoreWrite("attr/os/spmajor", Environment.OSVersion.ServicePack);

            h.StoreWrite("data/os_distro", "Windows");

            h.StoreWrite("data/os_majorver", ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Major));
            h.StoreWrite("data/os_minorver", ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Minor));
            h.StoreWrite("data/os_buildver", ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Build));

            var uname = ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Major) +
                "." +
                ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Minor) +
                "." +
                ServerUtils.NormalizeVersion(Environment.OSVersion.Version.Build);
            if (osInfo.WindowsRevision.HasValue) {
                uname += "." + osInfo.WindowsRevision.ToString();
            }
            h.StoreWrite("data/os_uname", uname);

            h.StoreWrite("data/os_name", osInfo.Caption);
            h.StoreWrite("data/host_name", Environment.MachineName);
            h.StoreWrite("data/host_name_dns", Dns.GetHostName());
            if (!string.IsNullOrEmpty(osInfo.DomainNameDns)) {
                h.StoreWrite("data/domain", osInfo.DomainNameDns);
            } else {
                h.StoreRemove("data/domain");
            }
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
