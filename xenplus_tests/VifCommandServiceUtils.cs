using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using XenPlus.VifConfigure;

sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T> where T : class {
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

static class VifCommandServiceUtils {
    public const uint TargetInterface = 17;
    public const uint OtherInterface = 29;

    public static VifCommandService CreateService(bool clearDns = false) {
        return new(
            new StaticOptionsMonitor<VifConfigureOptions>(
                new() {
                    ClearDnsOnEmptyDnsSetting = clearDns,
                }),
            "netsh.exe");
    }

    public static MIB_IF_ROW2 InterfaceRow() {
        return new() {
            InterfaceIndex = TargetInterface
        };
    }

    public static VifConfigurationIPv4Static IPv4Static(
        string address,
        int prefix,
        List<IPAddress>? dns,
        IPAddress? gateway = null) {
        return new() {
            StorePath = "vif",
            Mac = "00:11:22:33:44:55",
            Address = [
                new() {
                    Address = IPAddress.Parse(address),
                    Prefix = prefix,
                }
            ],
            Gateway = gateway,
            Dns = dns,
        };
    }

    public static VifConfigurationIPv6Static IPv6Static(
        string address,
        int prefix,
        IPAddress? gateway,
        List<IPAddress>? dns) {
        return new() {
            StorePath = "vif",
            Mac = "00:11:22:33:44:55",
            Address = [
                new() {
                    Address = IPAddress.Parse(address),
                    Prefix = prefix,
                }
            ],
            Gateway = gateway,
            Dns = dns,
        };
    }

    public static MIB_UNICASTIPADDRESS_ROW AddressRow(
        uint interfaceIndex,
        string address,
        bool isManual = false,
        bool isDhcp = false) {
        var prefixOrigin = NL_PREFIX_ORIGIN.IpPrefixOriginRouterAdvertisement;
        var suffixOrigin = NL_SUFFIX_ORIGIN.IpSuffixOriginOther;
        if (isDhcp) {
            prefixOrigin = NL_PREFIX_ORIGIN.IpPrefixOriginDhcp;
            suffixOrigin = NL_SUFFIX_ORIGIN.IpSuffixOriginDhcp;
        } else if (isManual) {
            prefixOrigin = NL_PREFIX_ORIGIN.IpPrefixOriginManual;
            suffixOrigin = NL_SUFFIX_ORIGIN.IpSuffixOriginManual;
        }

        return new() {
            Address = SocketAddress(IPAddress.Parse(address)),
            InterfaceIndex = interfaceIndex,
            PrefixOrigin = prefixOrigin,
            SuffixOrigin = suffixOrigin,
            OnLinkPrefixLength = address.Contains(':') ? (byte)64 : (byte)24,
        };
    }

    public static MIB_IPFORWARD_ROW2 RouteRow(
        uint interfaceIndex,
        string destination,
        byte prefix,
        string nextHop,
        bool isManual = false) {
        return new() {
            InterfaceIndex = interfaceIndex,
            DestinationPrefix = new IP_ADDRESS_PREFIX {
                Prefix = SocketAddress(IPAddress.Parse(destination)),
                PrefixLength = prefix,
            },
            NextHop = SocketAddress(IPAddress.Parse(nextHop)),
            Origin = isManual ? NL_ROUTE_ORIGIN.NlroManual : NL_ROUTE_ORIGIN.NlroRouterAdvertisement,
        };
    }

    public static SOCKADDR_INET SocketAddress(IPAddress address) {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily switch {
            AddressFamily.InterNetwork => new() {
                Ipv4 = new() {
                    sin_family = ADDRESS_FAMILY.AF_INET,
                    sin_addr = new() {
                        S_un = new() {
                            S_un_b = new() {
                                s_b1 = bytes[0],
                                s_b2 = bytes[1],
                                s_b3 = bytes[2],
                                s_b4 = bytes[3],
                            },
                        },
                    },
                },
            },
            AddressFamily.InterNetworkV6 => new() {
                Ipv6 = new() {
                    sin6_family = ADDRESS_FAMILY.AF_INET6,
                    sin6_addr = new() {
                        u = new() {
                            Byte = new ReadOnlySpan<byte>(bytes),
                        },
                    },
                },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(address)),
        };
    }
}
