using System.Net;
using System.Net.Sockets;
using XenPlus.XenIface;

namespace XenPlus.VifConfigure;

static class VifStore {
    internal const string StaticIpSetting = "static-ip-setting";

    internal static string VifConfigurationGetMac(XenIfaceHandle h, string vc) {
        return h.StoreReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "mac"));
    }

    static VifConfigurationIPv4Static ParseVifConfigurationIPv4Static(XenIfaceHandle h, string vc, string mac) {
        var rawAddress = h.StoreReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "address"));
        var splitAddress = rawAddress.Split('/', 2);
        ArgumentOutOfRangeException.ThrowIfNotEqual(splitAddress.Length, 2, $"Cannot parse CIDR from '{rawAddress}'");
        var address = new CIDR() {
            Address = IPAddress.Parse(splitAddress[0]),
            Prefix = int.Parse(splitAddress[1], System.Globalization.NumberStyles.None),
        };
        if (!address.Validate(AddressFamily.InterNetwork)) {
            throw new ArgumentException($"Address '{rawAddress}' is not an IPv4 address");
        }

        var rawGateway = h.StoreTryReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "gateway"));
        var gateway = rawGateway != null ? IPAddress.Parse(rawGateway) : null;
        if (gateway != null && gateway.AddressFamily != AddressFamily.InterNetwork) {
            throw new ArgumentException($"Gateway '{rawGateway}' is not an IPv4 address");
        }

        List<IPAddress>? dnsList = null;
        var dnsPath = StoreUtils.PathJoin(vc, StaticIpSetting, "dns");
        var dnsKeys = h.StoreTryDirectory(dnsPath);
        if (dnsKeys != null) {
            dnsList = [];
            foreach (var dnsKey in dnsKeys) {
                var rawDns = h.StoreTryReadStrict(StoreUtils.PathJoin(dnsPath, dnsKey));
                var dns = rawDns != null ? IPAddress.Parse(rawDns) : null;
                if (dns != null) {
                    if (dns.AddressFamily != AddressFamily.InterNetwork) {
                        throw new ArgumentException($"DNS '{rawDns}' is not an IPv4 address");
                    }
                    dnsList.Add(dns);
                }
            }
        }

        return new VifConfigurationIPv4Static() {
            StorePath = vc,
            Mac = mac,
            Address = [address],
            Gateway = gateway,
            Dns = dnsList,
        };
    }

    static VifConfigurationIPv6Static ParseVifConfigurationIPv6Static(XenIfaceHandle h, string vc, string mac) {
        var rawAddress = h.StoreReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "address6"));
        var splitAddress = rawAddress.Split('/', 2);
        ArgumentOutOfRangeException.ThrowIfNotEqual(splitAddress.Length, 2, $"Cannot parse CIDR from '{rawAddress}'");
        var address = new CIDR() {
            Address = IPAddress.Parse(splitAddress[0]),
            Prefix = int.Parse(splitAddress[1], System.Globalization.NumberStyles.None),
        };
        if (!address.Validate(AddressFamily.InterNetworkV6)) {
            throw new ArgumentException($"Address '{rawAddress}' is not an IPv6 address");
        }

        var rawGateway = h.StoreTryReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "gateway6"));
        var gateway = rawGateway != null ? IPAddress.Parse(rawGateway) : null;
        if (gateway != null && gateway.AddressFamily != AddressFamily.InterNetworkV6) {
            throw new ArgumentException($"Gateway '{rawGateway}' is not an IPv6 address");
        }

        List<IPAddress>? dnsList = null;
        var dnsPath = StoreUtils.PathJoin(vc, StaticIpSetting, "dns6");
        var dnsKeys = h.StoreTryDirectory(dnsPath);
        if (dnsKeys != null) {
            dnsList = [];
            foreach (var dnsKey in dnsKeys) {
                var rawDns = h.StoreTryReadStrict(StoreUtils.PathJoin(dnsPath, dnsKey));
                var dns = rawDns != null ? IPAddress.Parse(rawDns) : null;
                if (dns != null) {
                    if (dns.AddressFamily != AddressFamily.InterNetworkV6) {
                        throw new ArgumentException($"DNS '{rawDns}' is not an IPv6 address");
                    }
                    dnsList.Add(dns);
                }
            }
        }

        return new VifConfigurationIPv6Static() {
            StorePath = vc,
            Mac = mac,
            Address = [address],
            Gateway = gateway,
            Dns = dnsList,
        };
    }

    internal static VifConfigurationIPv4? ParseVifConfigurationIPv4(XenIfaceHandle h, string vc, string mac) {
        var enabled = h.StoreTryReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "enabled"));
        return enabled switch {
            "0" => new VifConfigurationIPv4None() {
                StorePath = vc,
                Mac = mac,
            },
            "1" => ParseVifConfigurationIPv4Static(h, vc, mac),
            "2" => new VifConfigurationIPv4Dhcp() {
                StorePath = vc,
                Mac = mac,
            },
            null or "" => null,
            _ => throw new ArgumentException($"Cannot parse enabled value '{enabled}'"),
        };
    }

    internal static VifConfigurationIPv6? ParseVifConfigurationIPv6(XenIfaceHandle h, string vc, string mac) {
        var enabled = h.StoreTryReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "enabled6"));
        return enabled switch {
            "0" => new VifConfigurationIPv6None() {
                StorePath = vc,
                Mac = mac,
            },
            "1" => ParseVifConfigurationIPv6Static(h, vc, mac),
            "2" => new VifConfigurationIPv6Autoconf() {
                StorePath = vc,
                Mac = mac,
            },
            null or "" => null,
            _ => throw new ArgumentException($"Cannot parse enabled6 value '{enabled}'"),
        };
    }

    internal static void AcknowledgeVifConfiguration(XenIfaceHandle h, string vc, string suffix) {
        try {
            h.StoreRemove(StoreUtils.PathJoin(vc, StaticIpSetting, "enabled" + suffix));
        } catch {
        }
    }

    internal static void RespondVifConfiguration(XenIfaceHandle h, string vc, IList<Exception> exs) {
        var errorCode = exs.FirstOrDefault() switch {
            null => 0,
            VifConfigureException vex => vex.ErrorCode,
            Exception ex => ex.HResult,
        };
        var msg = string.Join('\n', exs.Select(ex => ex.Message));
        try {
            h.StoreWrite(StoreUtils.PathJoin(vc, StaticIpSetting, "error-code"), errorCode.ToString());
            h.StoreWrite(StoreUtils.PathJoin(vc, StaticIpSetting, "error-msg"), msg);
        } catch {
        }
    }
}
