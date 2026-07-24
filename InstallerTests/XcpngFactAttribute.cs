using System.Runtime.CompilerServices;
using Xunit;

namespace InstallerTests {
    public sealed class XcpngFactAttribute : FactAttribute {
        public XcpngFactAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber) {
        }

        public XcpngFactAttribute() {
#pragma warning disable CS0162 // Unreachable code detected
            if (XenDriverUtils.VersionInfo.VendorPrefix != "XN") {
                Skip = "Not an XCP-ng build";
            }
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
}
