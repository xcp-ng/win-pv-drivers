using XenDriverUtils;
using Xunit;

namespace InstallerTests {
    public class XenDeviceInfoTest {
        [Fact]
        public void TestMatchHwidXenbus() {
            Assert.True(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_0001",
                checkKnown: true,
                checkIncompatible: false));
            Assert.False(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_0001",
                checkKnown: false,
                checkIncompatible: true));
            Assert.True(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_0001",
                checkKnown: true,
                checkIncompatible: true));
        }

        [Fact]
        public void TestMatchHwidSubstringXenbus() {
            Assert.True(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_0002&CC_FF80",
                checkKnown: true,
                checkIncompatible: false));
            Assert.False(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_0002&CC_FF80",
                checkKnown: false,
                checkIncompatible: true));
        }

        [Fact]
        public void TestMatchIncompatibleXenbus() {
            Assert.False(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_00FF&SUBSYS_00FF5853&REV_01",
                checkKnown: true,
                checkIncompatible: false));
            Assert.True(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_00FF&SUBSYS_00FF5853&REV_01",
                checkKnown: false,
                checkIncompatible: true));
        }

        [Fact]
        public void TestNoMatchIncompatibleXenbus() {
            Assert.False(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_0001&SUBSYS_00015853&REV_01",
                checkKnown: false,
                checkIncompatible: true));
        }

        [Fact]
        public void TestNoMatchC147() {
            // This is a "Virtualized Graphics Device", not a xen platform device or pvdevice. Therefore it must not be
            // marked as incompatible.
            Assert.False(XenDeviceInfo.KnownDevices["Xenbus"].MatchesId(
                "PCI\\VEN_5853&DEV_C147",
                checkKnown: false,
                checkIncompatible: true));
        }

        [XcpngFact]
        public void TestXcpngMatchHwid() {
            Assert.True(XenDeviceInfo.KnownDevices["Xeniface"].MatchesId(
                "XENBUS\\VEN_XN0002&DEV_IFACE&REV_0900000C",
                checkKnown: true,
                checkIncompatible: false));
            Assert.False(XenDeviceInfo.KnownDevices["Xeniface"].MatchesId(
                "XENBUS\\VEN_XN0002&DEV_IFACE&REV_0900000C",
                checkKnown: false,
                checkIncompatible: true));
        }

        [XcpngFact]
        public void TestXcpngMatchIncompatibleHwid() {
            // REV_08 is not compatible
            Assert.False(XenDeviceInfo.KnownDevices["Xeniface"].MatchesId(
                "XENBUS\\VEN_XN0002&DEV_IFACE&REV_0800000C",
                checkKnown: true,
                checkIncompatible: false));
            Assert.True(XenDeviceInfo.KnownDevices["Xeniface"].MatchesId(
                "XENBUS\\VEN_XN0002&DEV_IFACE&REV_0800000C",
                checkKnown: false,
                checkIncompatible: true));
        }

        [XcpngFact]
        public void TestXcpngMatchIncompatibleVendor() {
            Assert.False(XenDeviceInfo.KnownDevices["Xeniface"].MatchesId(
                "XENBUS\\VEN_XP0002&DEV_IFACE",
                checkKnown: true,
                checkIncompatible: false));
            Assert.True(XenDeviceInfo.KnownDevices["Xeniface"].MatchesId(
                "XENBUS\\VEN_XP0002&DEV_IFACE",
                checkKnown: false,
                checkIncompatible: true));
        }
    }
}
