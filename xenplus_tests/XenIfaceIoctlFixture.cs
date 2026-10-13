using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace XenPlus;

public sealed class XenIfaceIoctlFixture {
    internal (string Path, XenIfaceIoctlHandle Handle) OpenDevice() {
        var path = XenIfaceIoctlDeviceDiscovery.RequireActiveDevicePath();
        return (path, XenIfaceIoctlHandle.Open(path));
    }

    /// <remarks>
    /// Don't cache the domid because we could be migrated.
    /// </remarks>
    public ushort RequireDomainId() {
        const int DomainIdBufferLength = 6; // Five decimal ushort digits plus the terminating NUL.
        var (_, handle) = OpenDevice();
        using var handleLifetime = handle;
        var path = Encoding.ASCII.GetBytes("domid\0");
        using var request = handle.Ioctl(
            XenIfaceIoctl.IOCTL_XENIFACE_STORE_READ,
            path.Length,
            DomainIdBufferLength,
            buffer => { Marshal.Copy(path, 0, buffer, path.Length); });
        XenIfaceIoctlTestSupport.AssertSucceeded(request);

        var value = request.ReadAsciiString(DomainIdBufferLength);
        if (!ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var domainId)) {
            throw new InvalidDataException($"XenStore domid value is not a decimal ushort: '{value}'");
        }
        return domainId;
    }

    public static uint NewRequestId() {
        return unchecked((uint)Random.Shared.Next(1, int.MaxValue));
    }
}
