using System.Net;
using System.Runtime.InteropServices.Marshalling;
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

sealed class OSInfoFeature(
    XenIfaceSource _xi,
    [FromKeyedServices(ServiceKeys.WmiService_Root_CIMV2)] WmiService _cimv2,
    ILogger<OSInfoFeature> _logger) : BackgroundService {
    readonly Version _productVer = Version.Parse(VersionInfo.ProductVersion);
    readonly WmiOsInfo _osInfo = new(_cimv2, _logger);

    static int Normalize(int version) {
        return version >= 0 ? version : 0;
    }

    void OnResume(object? sender, XenIfaceResumedEventArgs args) {
        try {
            using var h = _xi.Lock();

            h.StoreWrite("attr/os/class", "Windows NT");

            // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/5.0/environment-osversion-returns-correct-version
            h.StoreWrite("attr/os/major", Normalize(Environment.OSVersion.Version.Major).ToString());
            h.StoreWrite("attr/os/minor", Normalize(Environment.OSVersion.Version.Minor).ToString());
            h.StoreWrite("attr/os/build", Normalize(Environment.OSVersion.Version.Build).ToString());
            h.StoreWrite("attr/os/platform", Environment.OSVersion.Platform.ToString());

            h.StoreWrite("data/os_distro", "Windows");

            h.StoreWrite("data/os_majorver", Normalize(Environment.OSVersion.Version.Major).ToString());
            h.StoreWrite("data/os_minorver", Normalize(Environment.OSVersion.Version.Minor).ToString());
            h.StoreWrite("data/os_buildver", Normalize(Environment.OSVersion.Version.Build).ToString());

            h.StoreWrite("data/os_uname", Environment.OSVersion.Version.ToString());

            h.StoreWrite("data/os_name", _osInfo.Caption);
            h.StoreWrite("data/host_name", Environment.MachineName);
            h.StoreWrite("data/host_name_dns", Dns.GetHostName());
            //h.StoreWrite("data/domain", _osInfo.Domain);

            h.StoreWrite("attr/PVAddons/MajorVersion", Normalize(_productVer.Major).ToString());
            h.StoreWrite("attr/PVAddons/MinorVersion", Normalize(_productVer.Minor).ToString());
            h.StoreWrite("attr/PVAddons/MicroVersion", Normalize(_productVer.Build).ToString());
            h.StoreWrite("attr/PVAddons/BuildVersion", Normalize(_productVer.Revision).ToString());
            h.StoreWrite("attr/PVAddons/Installed", "1");

            h.StoreWrite("data/updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(OSInfoFeature));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogTrace("Starting {}", nameof(OSInfoFeature));
        _xi.Resumed += OnResume;
        try {
            OnResume(null, new());
            await Task.Delay(Timeout.Infinite, stoppingToken);
        } finally {
            _xi.Resumed -= OnResume;
        }
    }
}
