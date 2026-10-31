using System.Globalization;

using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed class XenIfaceIoctlFixture {
    internal XenIfaceIoctlHandle OpenDevice() {
        return XenIfaceIoctlHandle.Open(XenIfaceIoctlDeviceDiscovery.RequireActiveDevicePath());
    }

    /// <remarks>
    /// Don't cache the domid because we could be migrated.
    /// </remarks>
    public ushort RequireDomainId() {
        const int DomainIdBufferLength = 6; // Five decimal ushort digits plus the terminating NUL.
        using var handle = OpenDevice();
        using var request = StoreIoctl(handle, XenIfaceIoctl.IOCTL_XENIFACE_STORE_READ, "domid\0", DomainIdBufferLength);
        AssertSucceeded(request);

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
