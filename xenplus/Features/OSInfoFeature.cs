using System.Net;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Extensions.Options;
using Windows.Win32;
using Windows.Win32.System.Wmi;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class WmiOsInfo {
    public string Caption { get; }

    static T? Get<T>(IWbemClassObject os, string propName) where T : class {
        var v = new ComVariant();
        os.Get(propName, 0, ref v);
        using (v) {
            return v.As<T>();
        }
    }

    public WmiOsInfo(WmiService cimv2, ILogger logger) {
        // if you see this, then COM initialization has failed...
        Caption = "Microsoft Windows (unknown edition)";

        try {
            if (cimv2.ExecQuery("SELECT * FROM Win32_OperatingSystem").FirstOrDefault() is not IWbemClassObject os) {
                return;
            }

            var caption = Get<string>(os, nameof(Caption));
            if (caption != null) {
                Caption = caption;
            }
        } catch (Exception ex) {
            logger.LogError(ex, "Cannot query OS info");
        }
    }
}

sealed class OSInfoOptions {
    public bool Enabled { get; set; } = true;
}

sealed class OSInfoFeature(
    IOptionsSnapshot<OSInfoOptions> _options,
    XenIfaceSource _xi,
    [FromKeyedServices(ServiceKeys.WmiService_Root_CIMV2)] WmiService _cimv2,
    ILogger<OSInfoFeature> _logger) : BackgroundService {
    readonly WmiOsInfo _osInfo = new(_cimv2, _logger);

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

            h.StoreWrite("data/os_name", _osInfo.Caption);
            h.StoreWrite("data/host_name", Environment.MachineName);
            h.StoreWrite("data/host_name_dns", Dns.GetHostName());
            //h.StoreWrite("data/domain", _osInfo.Domain);

            h.StoreWrite("data/updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(OSInfoFeature));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!_options.Value.Enabled) {
            return;
        }
        _logger.LogDebug("Starting {}", nameof(OSInfoFeature));
        _xi.Resumed += Report;
        try {
            Report(null, new());
            await Task.Delay(Timeout.Infinite, stoppingToken);
        } finally {
            _xi.Resumed -= Report;
        }
    }
}
