using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Windows.Win32;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class NetInfoOptions {
    public bool Enabled { get; set; } = true;
}

sealed class NetInfoFeature(
    IOptionsMonitor<NetInfoOptions> _options,
    XenIfaceSource _xi,
    ILogger<NetInfoFeature> _logger) : BackgroundService {
    static string MacToString(__byte_32 addr, int len) {
        return string.Join(':', addr
            .AsReadOnlySpan()[..len]
            .ToArray()
            .Select(x => x.ToString("x2")));
    }

    // cannot pass XenIfaceHandle across yield boundary, so allocate a collection
    static List<(string vifKey, string vifMac)> FindVifMacs(XenIfaceSource.XenIfaceHandle h) {
        List<(string vifKey, string vifMac)> result = [];
        var vifKeys = h.StoreDirectory("device/vif");
        foreach (var vifKey in vifKeys.Where(x => !string.IsNullOrEmpty(x))) {
            string? vifMac = null;
            try {
                vifMac = h.StoreRead($"device/vif/{vifKey}/mac");
            } catch {
            }
            if (vifMac != null) {
                result.Add((vifKey, vifMac));
            }
        }
        return result;
    }

    static void ReportVifOne(
        XenIfaceSource.XenIfaceHandle h,
        string vifKey,
        MIB_IF_ROW2 mibIf,
        NetworkInterface iface) {
        var aliasChars = mibIf.Alias.AsReadOnlySpan();
        var aliasEnd = aliasChars.IndexOf('\0');
        var alias = aliasEnd >= 0 ? aliasChars[..aliasEnd].ToString() : null;
        h.StoreWrite($"attr/vif/{vifKey}/name", alias, strict: false);

        h.StoreWrite($"attr/vif/{vifKey}/mac/0", MacToString(mibIf.PhysicalAddress, (int)mibIf.PhysicalAddressLength));

        int index4 = 0, index6 = 0;
        var ips = iface.GetIPProperties().UnicastAddresses;
        foreach (var ip in ips) {
            if (ip.Address.AddressFamily == AddressFamily.InterNetwork) {
                h.StoreWrite($"attr/vif/{vifKey}/ipv4/{index4++}", ip.Address.ToString());
            } else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6) {
                h.StoreWrite($"attr/vif/{vifKey}/ipv6/{index6++}", ip.Address.ToString());
            }
        }
    }

    static void TryFindReportVif(
        XenIfaceSource.XenIfaceHandle h,
        List<(string vifKey, string vifMac)> macs,
        MIB_IF_ROW2 mibIf,
        NetworkInterface[] ifaces) {
        if (mibIf.Type != PInvoke.IF_TYPE_ETHERNET_CSMACD) {
            return;
        }
        if (mibIf.OperStatus != IF_OPER_STATUS.IfOperStatusUp) {
            return;
        }
        if (mibIf.PhysicalAddressLength != 6) {
            return;
        }

        var permanentMac = MacToString(mibIf.PermanentPhysicalAddress, (int)mibIf.PhysicalAddressLength);
        foreach (var (vifKey, vifMac) in macs) {
            if (string.Equals(permanentMac, vifMac, StringComparison.OrdinalIgnoreCase)) {
                var iface = ifaces.FirstOrDefault(x => {
                    return Guid.TryParse(x.Id, out var ifaceGuid) && ifaceGuid == mibIf.InterfaceGuid;
                });
                if (iface == null) {
                    return;
                }
                ReportVifOne(h, vifKey, mibIf, iface);
                return;
            }
        }
    }

    void Report(object? sender, EventArgs args) {
        _logger.LogTrace("{}.{}", nameof(NetInfoFeature), nameof(Report));
        try {
            using var h = _xi.Lock();

            try {
                h.StoreRemove("attr/vif");
            } catch {
            }

            // assume that if enumeration of any sort failed, there's no vif

            List<(string vifKey, string vifMac)>? macs = null;
            try {
                macs = FindVifMacs(h);
            } catch {
            }
            if (macs == null || macs.Count == 0) {
                return;
            }

            var ifaces = NetworkInterface.GetAllNetworkInterfaces();
            using var mibIfTable = MibIfTable2SafeHandle.GetIfTable2();

            foreach (var mibIf in mibIfTable) {
                try {
                    TryFindReportVif(h, macs, mibIf, ifaces);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to report vif for luid {}", mibIf.InterfaceLuid);
                }
            }

            h.StoreWrite("data/updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(NetInfoFeature));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            if (!_options.CurrentValue.Enabled) {
                return;
            }
            _logger.LogDebug("Starting {}", nameof(NetInfoFeature));
            _xi.Resumed += Report;
            NetworkChange.NetworkAddressChanged += Report;
            try {
                Report(null, new());
                await Task.Delay(Timeout.Infinite, stoppingToken);
            } finally {
                NetworkChange.NetworkAddressChanged -= Report;
                _xi.Resumed -= Report;
            }
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            try {
                _logger.LogError(ex, "{} exited with exception", nameof(NetInfoFeature));
            } catch {
            }
            Environment.Exit(ex.HResult);
        }
    }
}
