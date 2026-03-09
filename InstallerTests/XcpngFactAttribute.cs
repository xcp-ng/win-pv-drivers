using Xunit;

namespace InstallerTests {
    public sealed class XcpngFactAttribute : FactAttribute {
        public XcpngFactAttribute() {
            if (XenDriverUtils.VersionInfo.VendorPrefix != "XN") {
                Skip = "Not an XCP-ng build";
            }
        }
    }
}
