using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Networking.ActiveDirectory;
using Windows.Win32.System.Wmi;

namespace XenPlus;

sealed class OSInfo {
    public required string Caption { get; init; }
    public required bool IsDomainJoined { get; init; }
    public required string DomainNameFlat { get; init; }
    public string? DomainNameDns { get; init; } = null;
    public string? DomainForestName { get; init; } = null;
}

/// <summary>
/// WMI is the only proper way to obtain the OS edition name.
/// </summary>
sealed class OSInfoService(
    [FromKeyedServices(ServiceKeys.WmiService_Root_CIMV2)] WmiService _cimv2,
    ILogger<OSInfoService> _logger) {
    static T? Get<T>(IWbemClassObject os, string propName) where T : class {
        var v = new ComVariant();
        os.Get(propName, 0, ref v);
        using (v) {
            return v.As<T>();
        }
    }

    public OSInfo Query() {
        // if you see this, then WMI initialization has failed...
        var Caption = "Microsoft Windows (unknown edition)";
        var IsDomainJoined = false;
        var DomainNameFlat = "";
        string? DomainNameDns = null;
        string? DomainForestName = null;

        try {
            if (_cimv2.ExecQuery("SELECT * FROM Win32_OperatingSystem").FirstOrDefault() is IWbemClassObject os) {
                Caption = Get<string>(os, nameof(Caption)) ?? Caption;
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Cannot query OS info");
        }

        try {
            using var domainInfo = DsRolePrimaryDomainInfoBasicSafeHandle.GetDsRolePrimaryDomainInfoBasic();
            IsDomainJoined = domainInfo.MachineRole == DSROLE_MACHINE_ROLE.DsRole_RoleStandaloneWorkstation ||
                domainInfo.MachineRole == DSROLE_MACHINE_ROLE.DsRole_RoleStandaloneServer;
            DomainNameFlat = domainInfo.DomainNameFlat;
            DomainNameDns = domainInfo.DomainNameDns;
            DomainForestName = domainInfo.DomainForestName;
        } catch (Exception ex) {
            _logger.LogError(ex, "Cannot query domain info");
        }

        return new OSInfo() {
            Caption = Caption,
            IsDomainJoined = IsDomainJoined,
            DomainNameFlat = DomainNameFlat,
            DomainNameDns = DomainNameDns,
            DomainForestName = DomainForestName,
        };
    }
}
