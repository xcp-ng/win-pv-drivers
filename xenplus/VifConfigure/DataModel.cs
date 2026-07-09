using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace XenPlus.VifConfigure;

struct CIDR {
    public required IPAddress Address { get; set; }
    public required int Prefix { get; set; }

    public readonly bool Validate(AddressFamily family) {
        if (Address.AddressFamily != family) {
            return false;
        }
        return Address.AddressFamily switch {
            AddressFamily.InterNetwork => Prefix >= 0 && Prefix <= 32,
            AddressFamily.InterNetworkV6 => Prefix >= 0 && Prefix <= 128,
            _ => false,
        };
    }
}

abstract class VifConfiguration {
    /// <summary>
    /// <c>xenserver/device/vif/&lt;key&gt;</c>
    /// </summary>
    public required string StorePath { get; set; }
    public required string Mac { get; set; }
    /// <summary>
    /// For display purposes only.
    /// </summary>
    public virtual string Category => "unknown";
}

class VifConfigurationIPv4 : VifConfiguration {
    public override string Category => "noop IPv4";
}

sealed class VifConfigurationIPv4None : VifConfigurationIPv4 {
}

sealed class VifConfigurationIPv4Static : VifConfigurationIPv4 {
    public override string Category => "static IPv4";
    /// <summary>
    /// CIDRs
    /// </summary>
    public required List<CIDR> Address { get; set; }
    public required IPAddress? Gateway { get; set; }
}

sealed class VifConfigurationIPv4Dhcp : VifConfigurationIPv4 {
    public override string Category => "DHCP IPv4";
}

class VifConfigurationIPv6 : VifConfiguration {
    public override string Category => "noop IPv6";
}

sealed class VifConfigurationIPv6None : VifConfigurationIPv6 {
}

sealed class VifConfigurationIPv6Static : VifConfigurationIPv6 {
    public override string Category => "static IPv6";
    /// <summary>
    /// CIDRs
    /// </summary>
    public required List<CIDR> Address { get; set; }
    public required IPAddress? Gateway { get; set; }
}

sealed class VifConfigurationIPv6Autoconf : VifConfigurationIPv6 {
    public override string Category => "autoconf IPv4";
}

class VifConfigurationEqualityComparer : IEqualityComparer<VifConfiguration> {
    public bool Equals(VifConfiguration? x, VifConfiguration? y) {
        return ReferenceEquals(x, y) || (
            x?.GetType() == y?.GetType() && string.Equals(x?.Mac, y?.Mac, StringComparison.OrdinalIgnoreCase));
    }

    public int GetHashCode([DisallowNull] VifConfiguration obj) {
        return (obj.GetType(), obj.Mac.ToLowerInvariant()).GetHashCode();
    }
}
