using System.Diagnostics.CodeAnalysis;

namespace XenPlus;

static class RequiredConstants {
    public const string VendorKey = VersionInfo.VendorKey;
    [SuppressMessage("CodeQuality", "IDE0051", Justification = "static assertion")]
    const uint _assert_VendorKey = VendorKey != "" ? 0 : -1;
}
