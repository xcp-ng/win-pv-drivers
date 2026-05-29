using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.System.Wmi;

namespace XenPlus;

/// <summary>
/// WMI is the only proper way to obtain the OS edition name.
/// </summary>
sealed class WmiOsInfoService {
    public string Caption { get; }

    static T? Get<T>(IWbemClassObject os, string propName) where T : class {
        var v = new ComVariant();
        os.Get(propName, 0, ref v);
        using (v) {
            return v.As<T>();
        }
    }

    public WmiOsInfoService(
        [FromKeyedServices(ServiceKeys.WmiService_Root_CIMV2)] WmiService _cimv2,
        ILogger<WmiOsInfoService> _logger) {
        // if you see this, then WMI initialization has failed...
        Caption = "Microsoft Windows (unknown edition)";

        try {
            if (_cimv2.ExecQuery("SELECT * FROM Win32_OperatingSystem").FirstOrDefault() is not IWbemClassObject os) {
                return;
            }

            var caption = Get<string>(os, nameof(Caption));
            if (caption != null) {
                Caption = caption;
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Cannot query OS info");
        }
    }
}
