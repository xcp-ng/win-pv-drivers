using System.Runtime.InteropServices.Marshalling;
using System.Text.RegularExpressions;
using Microsoft.Win32;
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
    public int? LCUVer { get; init; } = null;
}

/// <summary>
/// WMI is the only proper way to obtain the OS edition name.
/// </summary>
sealed partial class OSInfoService(
    [FromKeyedServices(ServiceKeys.WmiService_Root_CIMV2)] WmiService _cimv2,
    ILogger<OSInfoService> _logger) {
    static T? Get<T>(IWbemClassObject os, string propName) where T : class {
        var v = new ComVariant();
        os.Get(propName, 0, ref v);
        using (v) {
            return v.As<T>();
        }
    }

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+\.([0-9]+)")]
    private static partial Regex LCUVerRegex { get; }

    static int? GetLCUVer() {
        // According to a MS guy, registry is the most reliable way to get the CurrentBuildNumber and LCUVer.
        // Not sure if RtlGetVersion reports the right build number that we want, but we'll see.
        using var versionKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (versionKey == null) {
            return null;
        }
        if (versionKey.GetValue("LCUVer") is not string lcuVer) {
            return null;
        }
        var match = LCUVerRegex.Match(lcuVer);
        if (!match.Success) {
            return null;
        }
        if (!int.TryParse(match.Groups[1].ValueSpan, out var buildNumber)) {
            return null;
        }
        return buildNumber;
    }

    public OSInfo Query() {
        // if you see this, then WMI initialization has failed...
        var Caption = "Microsoft Windows (unknown edition)";
        var IsDomainJoined = false;
        var DomainNameFlat = "";
        string? DomainNameDns = null;
        string? DomainForestName = null;
        int? LCUVer = null;

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

        try {
            LCUVer = GetLCUVer();
        } catch (Exception ex) {
            _logger.LogError(ex, "Cannot query build number info");
        }

        return new OSInfo() {
            Caption = Caption,
            IsDomainJoined = IsDomainJoined,
            DomainNameFlat = DomainNameFlat,
            DomainNameDns = DomainNameDns,
            DomainForestName = DomainForestName,
            LCUVer = LCUVer,
        };
    }
}
