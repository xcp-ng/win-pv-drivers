using System.ComponentModel;
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
            throw new ArgumentException($"IPv4 address '{rawAddress}' is not acceptable");
        }

        var rawGateway = h.StoreTryReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "gateway"));
        var gateway = rawGateway != null ? IPAddress.Parse(rawGateway) : null;
        if (gateway != null && gateway.AddressFamily != AddressFamily.InterNetwork) {
            throw new ArgumentException($"IPv4 gateway '{rawGateway}' is not acceptable");
        }

        return new VifConfigurationIPv4Static() {
            StorePath = vc,
            Mac = mac,
            Address = [address],
            Gateway = gateway,
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
            throw new ArgumentException($"IPv6 address '{rawAddress}' is not acceptable");
        }

        var rawGateway = h.StoreTryReadStrict(StoreUtils.PathJoin(vc, StaticIpSetting, "gateway6"));
        var gateway = rawGateway != null ? IPAddress.Parse(rawGateway) : null;
        if (gateway != null && gateway.AddressFamily != AddressFamily.InterNetworkV6) {
            throw new ArgumentException($"IPv6 gateway '{rawGateway}' is not acceptable");
        }

        return new VifConfigurationIPv6Static() {
            StorePath = vc,
            Mac = mac,
            Address = [address],
            Gateway = gateway,
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

    internal static void RespondVifConfiguration(XenIfaceHandle h, string vc, Exception? ex = null) {
        try {
            h.StoreRemove(StoreUtils.PathJoin(vc, StaticIpSetting, "enabled"));
        } catch {
        }
        try {
            h.StoreRemove(StoreUtils.PathJoin(vc, StaticIpSetting, "enabled6"));
        } catch {
        }
        if (ex != null) {
            try {
                h.StoreWrite(StoreUtils.PathJoin(vc, StaticIpSetting, "error-code"), ex.HResult.ToString());
                h.StoreWrite(StoreUtils.PathJoin(vc, StaticIpSetting, "error-msg"), ex.Message);
            } catch {
            }
        } else {
            try {
                h.StoreWrite(StoreUtils.PathJoin(vc, StaticIpSetting, "error-code"), "0");
                h.StoreWrite(StoreUtils.PathJoin(vc, StaticIpSetting, "error-msg"), "");
            } catch {
            }
        }
    }
}
