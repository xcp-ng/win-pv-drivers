using System.Net;
using Microsoft.Extensions.Options;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

namespace XenPlus.VifConfigure;

/// <summary>
/// A realizer for <see cref="VifConfiguration"/>.
/// </summary>
class VifCommandService(IOptionsMonitor<VifConfigureOptions> _options, string _netshPath) {
    string NetshArgumentDnsValidate => _options.CurrentValue.ValidateDnsServers ? "validate=yes" : "validate=no";

    public IEnumerable<(string fileName, List<string> arguments)> GetCommandsConfigv4(
        MIB_IF_ROW2 mibIf,
        IReadOnlyList<MIB_UNICASTIPADDRESS_ROW> mibIPTable,
        VifConfigurationIPv4 config) {
        var interfaceIndex = mibIf.InterfaceIndex.ToString();

        if (config is VifConfigurationIPv4Dhcp) {
            if (!mibIPTable.HasDhcpAddress(mibIf.InterfaceIndex, ADDRESS_FAMILY.AF_INET)) {
                yield return (_netshPath, ["interface", "ipv4", "set", "address", interfaceIndex, "source=dhcp"]);
            }
            yield return (_netshPath, [
                "interface",
                "ipv4",
                "set",
                "dnsservers",
                interfaceIndex,
                "source=dhcp",
                NetshArgumentDnsValidate,
            ]);
        } else if (config is VifConfigurationIPv4Static staticv4) {
            var address = staticv4.Address[0];
            yield return (_netshPath, [
                "interface",
                "ipv4",
                "set",
                "address",
                interfaceIndex,
                "source=static",
                $"address={address.Address.ToStringWithoutScopeId()}/{address.Prefix}",
                $"gateway={staticv4.Gateway?.ToString() ?? "none"}",
            ]);

            var dns = staticv4.Dns;
            if (dns != null) {
                if (dns.Count > 0) {
                    for (int i = 0; i < dns.Count; i++) {
                        List<string> dnsArgs = [
                            "interface",
                            "ipv4",
                        ];
                        if (i == 0) {
                            dnsArgs.AddRange([
                                "set",
                                "dnsservers",
                                interfaceIndex,
                                "source=static",
                            ]);
                        } else {
                            dnsArgs.AddRange([
                                "add",
                                "dnsservers",
                                interfaceIndex,
                            ]);
                        }
                        dnsArgs.AddRange([$"address={dns[i]}", NetshArgumentDnsValidate]);
                        yield return (_netshPath, dnsArgs);
                    }
                } else if (_options.CurrentValue.ClearDnsOnEmptyDnsSetting) {
                    yield return (_netshPath, ["interface", "ipv4", "set", "dnsservers", interfaceIndex, "address=none"]);
                }
            }
        }
    }

    public IEnumerable<(string fileName, List<string> arguments)> GetCommandsConfigv6(
        MIB_IF_ROW2 mibIf,
        IReadOnlyList<MIB_UNICASTIPADDRESS_ROW> mibIPTable,
        IReadOnlyList<MIB_IPFORWARD_ROW2> mibRouteTable,
        VifConfigurationIPv6 config) {
        var interfaceIndex = mibIf.InterfaceIndex.ToString();

        if (config is VifConfigurationIPv6Autoconf) {
            foreach (var (address, isManual) in mibIPTable.GetUnicastAddresses(
                mibIf.InterfaceIndex,
                ADDRESS_FAMILY.AF_INET6)) {
                if (isManual) {
                    yield return (_netshPath, [
                        "interface",
                        "ipv6",
                        "delete",
                        "address",
                        interfaceIndex,
                        address.Address.ToStringWithoutScopeId(),
                    ]);
                }
            }
            foreach (var (gateway, isManual) in mibRouteTable.GetDefaultRoute(
                mibIf.InterfaceIndex,
                ADDRESS_FAMILY.AF_INET6)) {
                if (isManual) {
                    yield return (_netshPath, [
                        "interface",
                        "ipv6",
                        "delete",
                        "route",
                        "::/0",
                        interfaceIndex,
                        gateway.ToStringWithoutScopeId(),
                    ]);
                }
            }
            yield return (_netshPath, [
                "interface",
                "ipv6",
                "set",
                "dnsservers",
                interfaceIndex,
                "source=dhcp",
                NetshArgumentDnsValidate,
            ]);

        } else if (config is VifConfigurationIPv6Static staticv6) {
            var address = staticv6.Address[0];
            var foundExistingAddress = false;
            foreach (var (existing, isManual) in mibIPTable.GetUnicastAddresses(
                mibIf.InterfaceIndex,
                ADDRESS_FAMILY.AF_INET6)) {
                if (!isManual) {
                    continue;
                }
                if (existing.Address.EqualsWithoutScopeId(address.Address) &&
                    existing.Prefix == address.Prefix) {
                    foundExistingAddress = true;
                    continue;
                }
                yield return (_netshPath, [
                    "interface",
                    "ipv6",
                    "delete",
                    "address",
                    interfaceIndex,
                    existing.Address.ToStringWithoutScopeId(),
                ]);
            }
            if (!foundExistingAddress) {
                yield return (_netshPath, [
                    "interface",
                    "ipv6",
                    "add",
                    "address",
                    interfaceIndex,
                    $"{address.Address.ToStringWithoutScopeId()}/{address.Prefix}",
                ]);
            }

            var foundExistingGateway = false;
            foreach (var (existingGateway, isManual) in mibRouteTable.GetDefaultRoute(
                mibIf.InterfaceIndex,
                ADDRESS_FAMILY.AF_INET6)) {
                if (isManual &&
                    staticv6.Gateway is IPAddress gateway &&
                    existingGateway.Equals(gateway)) {
                    foundExistingGateway = true;
                    continue;
                }
                // Unlike the IP list, we want to delete gateways regardless of whether they're manual or not.
                yield return (_netshPath, [
                    "interface",
                    "ipv6",
                    "delete",
                    "route",
                    "::/0",
                    interfaceIndex,
                    existingGateway.ToStringWithoutScopeId(),
                ]);
            }
            if (staticv6.Gateway is IPAddress newGateway && !foundExistingGateway) {
                yield return (_netshPath, [
                    "interface",
                    "ipv6",
                    "add",
                    "route",
                    "::/0",
                    interfaceIndex,
                    newGateway.ToStringWithoutScopeId(),
                ]);
            }

            var dns = staticv6.Dns;
            if (dns != null) {
                if (dns.Count > 0) {
                    for (int i = 0; i < dns.Count; i++) {
                        List<string> dnsArgs = [
                            "interface",
                            "ipv6",
                        ];
                        if (i == 0) {
                            dnsArgs.AddRange([
                                "set",
                                "dnsservers",
                                interfaceIndex,
                                "source=static",
                            ]);
                        } else {
                            dnsArgs.AddRange([
                                "add",
                                "dnsservers",
                                interfaceIndex,
                            ]);
                        }
                        dnsArgs.AddRange([$"address={dns[i]}", NetshArgumentDnsValidate]);
                        yield return (_netshPath, dnsArgs);
                    }
                } else if (_options.CurrentValue.ClearDnsOnEmptyDnsSetting) {
                    yield return (_netshPath, ["interface", "ipv6", "set", "dnsservers", interfaceIndex, "address=none"]);
                }
            }
        }
    }

}
