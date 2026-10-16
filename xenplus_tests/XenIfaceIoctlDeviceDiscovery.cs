using System.Security.Principal;

namespace XenPlus;

/// <summary>Test-only direct interface discovery for raw IOCTL cases.</summary>
static class XenIfaceIoctlDeviceDiscovery {
    static readonly Guid InterfaceClass = new(
        0xb2cfb085,
        0xaa5e,
        0x47e1,
        0x8b, 0xf7, 0x97, 0x93, 0xf3, 0x15, 0x45, 0x65);

    static bool IsAdministrator() {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string RequireActiveDevicePath() {
        Assert.SkipUnless(IsAdministrator(), "XenIface API tests require an elevated test process");

        var path = Cfgmgr32.GetDeviceInterfaces(InterfaceClass).FirstOrDefault();
        if (path == null) {
            Assert.Skip("No active XenIface device was detected");
        }
        return path;
    }
}
