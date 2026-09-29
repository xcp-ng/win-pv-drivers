using System.Net;
using XenPlus.VifConfigure;

using static VifCommandServiceUtils;

namespace XenPlus;

public class VifCommandServiceTests {
    [Fact]
    public void IPv4DhcpCommandsIgnoreOtherInterfacesAndFamilies() {
        var service = CreateService();
        var unrelatedDhcpRows = new[] {
            AddressRow(OtherInterface, "192.0.2.15", isDhcp: true),
            AddressRow(TargetInterface, "2001:db8::15", isDhcp: true),
        };
        var config = new VifConfigurationIPv4Dhcp {
            StorePath = "vif",
            Mac = "00:11:22:33:44:55"
        };

        var commands = service.GetCommandsConfigv4(InterfaceRow(), unrelatedDhcpRows, config).ToList();

        var baseline = service.GetCommandsConfigv4(InterfaceRow(), [], config).ToList();
        AssertCommandsEqual(baseline, commands);
        AssertNoFamily(commands, "ipv6");

        var matchingDhcpRow = AddressRow(TargetInterface, "192.0.2.17", isDhcp: true);
        var commandsWithExistingLease = service.GetCommandsConfigv4(
            InterfaceRow(),
            unrelatedDhcpRows.Append(matchingDhcpRow).ToArray(),
            config).ToList();
        var baselineWithExistingLease = service.GetCommandsConfigv4(
            InterfaceRow(),
            [matchingDhcpRow],
            config).ToList();

        AssertCommandsEqual(baselineWithExistingLease, commandsWithExistingLease);
        AssertNoFamily(commandsWithExistingLease, "ipv6");
    }

    [Fact]
    public void IPv4StaticCommandsIgnoreOtherInterfacesAndFamilies() {
        var service = CreateService();
        var mixedRows = new[] {
            AddressRow(TargetInterface, "2001:db8::17", isManual: true),
            AddressRow(OtherInterface, "192.0.2.29", isManual: true),
        };
        var config = IPv4Static(
            "192.0.2.17", 24,
            dns: [
                IPAddress.Parse("192.0.2.53"),
                IPAddress.Parse("192.0.2.54"),
            ],
            gateway: IPAddress.Parse("192.0.2.1"));

        var commands = service.GetCommandsConfigv4(InterfaceRow(), mixedRows, config).ToList();

        var baseline = service.GetCommandsConfigv4(InterfaceRow(), [], config).ToList();
        AssertCommandsEqual(baseline, commands);
        AssertNoFamily(commands, "ipv6");
    }

    [Fact]
    public void IPv4NoneProducesNoCommandsEvenWithMixedExistingState() {
        var service = CreateService();
        var mixedRows = new[] {
            AddressRow(TargetInterface, "192.0.2.17", isManual: true),
            AddressRow(OtherInterface, "2001:db8::29", isDhcp: true),
        };
        var config = new VifConfigurationIPv4None {
            StorePath = "vif",
            Mac = "00:11:22:33:44:55",
        };

        Assert.Empty(service.GetCommandsConfigv4(InterfaceRow(), mixedRows, config));
    }

    [Fact]
    public void IPv6AutoconfCommandsIgnoreUnrelatedAndAutomaticState() {
        const string manualAddress = "2001:db8:1::10";
        const string automaticAddress = "2001:db8:1::20";
        const string otherInterfaceAddress = "2001:db8:2::29";
        const string manualGateway = "2001:db8:1::1";
        const string automaticGateway = "2001:db8:1::2";
        const string otherInterfaceGateway = "2001:db8:2::1";
        const string nonDefaultGateway = "2001:db8:ffff::1";

        var service = CreateService();
        var mixedRows = new[] {
            AddressRow(TargetInterface, manualAddress, isManual: true),
            AddressRow(TargetInterface, automaticAddress),
            AddressRow(OtherInterface, otherInterfaceAddress, isManual: true),
            AddressRow(TargetInterface, "192.0.2.17", isManual: true),
        };
        var mixedRoutes = new[] {
            RouteRow(TargetInterface, "::", 0, manualGateway, isManual: true),
            RouteRow(TargetInterface, "::", 0, automaticGateway),
            RouteRow(OtherInterface, "::", 0, otherInterfaceGateway, isManual: true),
            RouteRow(TargetInterface, "2001:db8:10::", 64, nonDefaultGateway, isManual: true),
            RouteRow(TargetInterface, "0.0.0.0", 0, "192.0.2.1", isManual: true),
        };
        var config = new VifConfigurationIPv6Autoconf {
            StorePath = "vif",
            Mac = "00:11:22:33:44:55",
        };

        var commands = service.GetCommandsConfigv6(InterfaceRow(), mixedRows, mixedRoutes, config).ToList();

        var baseline = service.GetCommandsConfigv6(
            InterfaceRow(),
            [AddressRow(TargetInterface, manualAddress, isManual: true)],
            [RouteRow(TargetInterface, "::", 0, manualGateway, isManual: true)],
            config).ToList();

        AssertCommandsEqual(baseline, commands);
        AssertNoFamily(commands, "ipv4");
    }

    [Fact]
    public void IPv6StaticCommandsIgnoreUnrelatedStateAndAutomaticAddresses() {
        const string automaticAddress = "2001:db8:3::20";
        const string manualAddress = "2001:db8:3::10";
        const string unwantedManualGateway = "2001:db8:3::3";
        const string unwantedRouterAdvertisementGateway = "2001:db8:3::2";
        const string otherInterfaceGateway = "2001:db8:4::1";
        const string nonDefaultGateway = "2001:db8:ffff::1";

        var service = CreateService();
        var mixedRows = new[] {
            AddressRow(TargetInterface, automaticAddress),
            AddressRow(TargetInterface, manualAddress, isManual: true),
            AddressRow(OtherInterface, "2001:db8:4::20", isManual: true),
            AddressRow(TargetInterface, "192.0.2.17", isManual: true),
        };
        var mixedRoutes = new[] {
            RouteRow(TargetInterface, "::", 0, unwantedManualGateway, isManual: true),
            RouteRow(TargetInterface, "::", 0, unwantedRouterAdvertisementGateway),
            RouteRow(OtherInterface, "::", 0, otherInterfaceGateway, isManual: true),
            RouteRow(TargetInterface, "2001:db8:10::", 64, nonDefaultGateway, isManual: true),
            RouteRow(TargetInterface, "0.0.0.0", 0, "192.0.2.1", isManual: true),
        };
        var config = IPv6Static(
            "2001:db8:3::30", 64, IPAddress.Parse("2001:db8:3::1"),
            dns: [
                IPAddress.Parse("2001:db8::53"),
                IPAddress.Parse("2001:db8::54"),
            ]);

        var commands = service.GetCommandsConfigv6(InterfaceRow(), mixedRows, mixedRoutes, config).ToList();

        var baseline = service.GetCommandsConfigv6(
            InterfaceRow(),
            [AddressRow(TargetInterface, manualAddress, isManual: true)],
            [
                RouteRow(TargetInterface, "::", 0, unwantedManualGateway, isManual: true),
                RouteRow(TargetInterface, "::", 0, unwantedRouterAdvertisementGateway),
            ],
            config).ToList();

        AssertCommandsEqual(baseline, commands);
        AssertNoFamily(commands, "ipv4");
    }

    [Fact]
    public void IPv6NoneProducesNoCommandsEvenWithMixedExistingState() {
        var service = CreateService();
        var mixedRows = new[] {
            AddressRow(TargetInterface, "2001:db8::17", isManual: true),
            AddressRow(OtherInterface, "192.0.2.29", isDhcp: true),
        };
        var mixedRoutes = new[] {
            RouteRow(TargetInterface, "::", 0, "2001:db8::1", isManual: true),
            RouteRow(OtherInterface, "0.0.0.0", 0, "192.0.2.1", isManual: true),
        };
        var config = new VifConfigurationIPv6None {
            StorePath = "vif",
            Mac = "00:11:22:33:44:55",
        };

        Assert.Empty(service.GetCommandsConfigv6(InterfaceRow(), mixedRows, mixedRoutes, config));
    }

    [Fact]
    public void IPv4NullDnsIgnoresClearDnsOnEmptyDnsSetting() {
        var config = IPv4Static("192.0.2.17", 24, dns: null);
        var withoutClearing = CreateService(clearDns: false).GetCommandsConfigv4(
            InterfaceRow(), [], config).ToList();
        var withClearing = CreateService(clearDns: true).GetCommandsConfigv4(
            InterfaceRow(), [], config).ToList();

        AssertCommandsEqual(withoutClearing, withClearing);
        AssertNoFamily(withClearing, "ipv6");
    }

    [Fact]
    public void IPv6NullDnsIgnoresClearDnsOnEmptyDnsSetting() {
        var config = IPv6Static("2001:db8::17", 64, gateway: null, dns: null);
        var withoutClearing = CreateService(clearDns: false).GetCommandsConfigv6(
            InterfaceRow(), [], [], config).ToList();
        var withClearing = CreateService(clearDns: true).GetCommandsConfigv6(
            InterfaceRow(), [], [], config).ToList();

        AssertCommandsEqual(withoutClearing, withClearing);
        AssertNoFamily(withClearing, "ipv4");
    }

    static void AssertNoFamily(
        IEnumerable<(string fileName, List<string> arguments)> commands,
        string forbiddenFamily) {
        foreach (var (_, arguments) in commands) {
            Assert.DoesNotContain(arguments, argument =>
                string.Equals(argument, forbiddenFamily, StringComparison.OrdinalIgnoreCase));
        }
    }

    static void AssertCommandsEqual(
        List<(string fileName, List<string> arguments)> expected,
        List<(string fileName, List<string> arguments)> actual) {
        // Compare opaque outputs from related inputs, without interpreting command syntax.
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++) {
            Assert.Equal(expected[i].fileName, actual[i].fileName);
            Assert.Equal<string>(expected[i].arguments, actual[i].arguments);
        }
    }
}
