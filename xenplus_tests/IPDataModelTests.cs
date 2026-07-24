using System.Net;
using System.Net.Sockets;
using XenPlus.VifConfigure;

namespace XenPlus;

public class IPDataModelTests {
    [Theory]
    [InlineData(0, true)]
    [InlineData(32, true)]
    [InlineData(-1, false)]
    [InlineData(33, false)]
    public void CidrValidatesIPv4Prefix(int prefix, bool expected) {
        var cidr = new CIDR {
            Address = IPAddress.Parse("192.0.2.1"),
            Prefix = prefix,
        };

        Assert.Equal(expected, cidr.Validate(AddressFamily.InterNetwork));
        Assert.False(cidr.Validate(AddressFamily.InterNetworkV6));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(128, true)]
    [InlineData(-1, false)]
    [InlineData(129, false)]
    public void CidrValidatesIPv6Prefix(int prefix, bool expected) {
        var cidr = new CIDR {
            Address = IPAddress.Parse("2001:db8::1"),
            Prefix = prefix,
        };

        Assert.Equal(expected, cidr.Validate(AddressFamily.InterNetworkV6));
        Assert.False(cidr.Validate(AddressFamily.InterNetwork));
    }

    [Fact]
    public void ConfigurationEqualityUsesConcreteTypeAndCaseInsensitiveMac() {
        var comparer = new VifConfigurationEqualityComparer();
        var first = new VifConfigurationIPv4Dhcp {
            StorePath = "first",
            Mac = "AA:BB:CC:DD:EE:FF",
        };
        var sameIdentity = new VifConfigurationIPv4Dhcp {
            StorePath = "different-path",
            Mac = "aa:bb:cc:dd:ee:ff",
        };
        var differentType = new VifConfigurationIPv4None {
            StorePath = "first",
            Mac = "AA:BB:CC:DD:EE:FF",
        };
        var differentMac = new VifConfigurationIPv4Dhcp {
            StorePath = "first",
            Mac = "00:11:22:33:44:55",
        };

        Assert.True(comparer.Equals(first, sameIdentity));
        Assert.Equal(comparer.GetHashCode(first), comparer.GetHashCode(sameIdentity));
        Assert.False(comparer.Equals(first, differentType));
        Assert.False(comparer.Equals(first, differentMac));
        Assert.False(comparer.Equals(first, null));
        Assert.True(comparer.Equals(null, null));
    }

    [Fact]
    public void IPv6ScopeIdIsIgnoredByScopeIndependentHelpers() {
        var bytes = IPAddress.Parse("fe80::1").GetAddressBytes();
        var first = new IPAddress(bytes, 1);
        var second = new IPAddress(bytes, 42);
        var different = IPAddress.Parse("fe80::2");

        Assert.True(first.EqualsWithoutScopeId(second));
        Assert.False(first.EqualsWithoutScopeId(different));
        Assert.Equal("fe80::1", first.ToStringWithoutScopeId());
    }

    [Fact]
    public void ScopeIndependentEqualityStillRequiresMatchingAddressFamily() {
        var ipv4 = IPAddress.Parse("192.0.2.1");
        var ipv6 = IPAddress.Parse("::ffff:192.0.2.1");

        Assert.False(ipv4.EqualsWithoutScopeId(ipv6));
    }
}
