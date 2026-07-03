using System.Net;
using System.Net.Sockets;
using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

namespace XenPlus.VifConfigure;

static class IPHelperExtensions {
    internal static string MacToString(__byte_32 addr, int len) {
        return string.Join(':', addr
            .AsReadOnlySpan()[..len]
            .ToArray()
            .Select(x => x.ToString("x2")));
    }

    internal static MIB_IF_ROW2? FindIfaceRow(this MibIfTable2SafeHandle mibIfTable, string mac) {
        foreach (var mibIf in mibIfTable) {
            if (mibIf.Type != PInvoke.IF_TYPE_ETHERNET_CSMACD) {
                continue;
            }
            if (!mibIf.InterfaceAndOperStatusFlags.HardwareInterface ||
                // check needed to rule out "absent" adapters
                !mibIf.InterfaceAndOperStatusFlags.ConnectorPresent) {
                continue;
            }
            if (mibIf.PhysicalAddressLength != 6) {
                continue;
            }

            var permanentMac = MacToString(mibIf.PermanentPhysicalAddress, (int)mibIf.PhysicalAddressLength);
            if (permanentMac.Equals(mac, StringComparison.OrdinalIgnoreCase)) {
                return mibIf;
            }
        }
        return null;
    }

    internal static byte[]? GetBytes(this SOCKADDR_INET sockaddr) {
        if (sockaddr.si_family == ADDRESS_FAMILY.AF_INET) {
            var bytes = sockaddr.Ipv4.sin_addr.S_un.S_un_b;
            return [bytes.s_b1, bytes.s_b2, bytes.s_b3, bytes.s_b4];
        } else if (sockaddr.si_family == ADDRESS_FAMILY.AF_INET6) {
            return sockaddr.Ipv6.sin6_addr.u.Byte.AsReadOnlySpan().ToArray();
        } else {
            return null;
        }
    }

    internal static IPAddress? ToIPAddress(this SOCKADDR_INET sockaddr) {
        var bytes = sockaddr.GetBytes();
        if (bytes == null) {
            return null;
        }
        return sockaddr.si_family switch {
            ADDRESS_FAMILY.AF_INET => new IPAddress(bytes),
            ADDRESS_FAMILY.AF_INET6 => new IPAddress(bytes, sockaddr.Ipv6.sin6_scope_id),
            _ => null,
        };
    }

    internal static bool AddressEquals(SOCKADDR_INET sockaddr, IPAddress ipaddr) {
        if ((sockaddr.si_family == ADDRESS_FAMILY.AF_INET && ipaddr.AddressFamily == AddressFamily.InterNetwork) ||
            (sockaddr.si_family == ADDRESS_FAMILY.AF_INET6 && ipaddr.AddressFamily == AddressFamily.InterNetworkV6)) {
            return ipaddr.GetAddressBytes().SequenceEqual(sockaddr.GetBytes());
        }
        return false;
    }

    internal static bool HasDhcpAddress(
        this MibUnicastIpAddressTableSafeHandle mibIPTable,
        uint interfaceIndex,
        ADDRESS_FAMILY family) {
        return mibIPTable.Any(row =>
            row.InterfaceIndex == interfaceIndex &&
            row.Address.si_family == family &&
            (row.PrefixOrigin == NL_PREFIX_ORIGIN.IpPrefixOriginDhcp ||
                row.SuffixOrigin == NL_SUFFIX_ORIGIN.IpSuffixOriginDhcp));
    }

    internal static bool HasUnicastAddress(
        this MibUnicastIpAddressTableSafeHandle mibIPTable,
        uint interfaceIndex,
        CIDR address) {
        return mibIPTable.Any(row =>
            row.InterfaceIndex == interfaceIndex &&
            row.OnLinkPrefixLength == address.Prefix &&
            AddressEquals(row.Address, address.Address));
    }

    internal static bool HasDefaultRoute(
        this MibIpForwardTable2SafeHandle mibRouteTable,
        uint interfaceIndex,
        IPAddress gateway) {
        ADDRESS_FAMILY family;
        switch (gateway.AddressFamily) {
            case AddressFamily.InterNetwork:
                family = ADDRESS_FAMILY.AF_INET;
                break;
            case AddressFamily.InterNetworkV6:
                family = ADDRESS_FAMILY.AF_INET6;
                break;
            default:
                return false;
        }

        return mibRouteTable.Any(row =>
            row.InterfaceIndex == interfaceIndex &&
            row.DestinationPrefix.Prefix.si_family == family &&
            row.DestinationPrefix.PrefixLength == 0 &&
            AddressEquals(row.NextHop, gateway));
    }

    internal static List<CIDR> GetManualUnicastAddresses(
        this MibUnicastIpAddressTableSafeHandle mibIPTable,
        uint interfaceIndex,
        ADDRESS_FAMILY family) {
        return mibIPTable.Where(row =>
            row.InterfaceIndex == interfaceIndex &&
            row.Address.si_family == family &&
            (row.PrefixOrigin == NL_PREFIX_ORIGIN.IpPrefixOriginManual ||
                row.SuffixOrigin == NL_SUFFIX_ORIGIN.IpSuffixOriginManual)
        ).Select(row => new CIDR() {
            Address = Check.Unwrap(row.Address.ToIPAddress()),
            Prefix = row.OnLinkPrefixLength,
        }).ToList();
    }
}
