using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class GnttabIoctlTests {
    [Theory(Explicit = true)]
    [InlineData(1u)]
    [Trait("CaseId", "G06-V1-SAME-ID-OWNER")]
    [Trait("Profile", "local")]
    [Trait("ExternalCoordination", "true")]
    public void G06V1OwnerRetainsItsRequestWhenContenderUsesTheSameId(uint requestId) {
        using var coordination = XenIfaceIoctlCoordination.Open("g06-v1");
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var grant = PermitV1(handle, domain, requestId);

        coordination.SignalOwnerReady();
        coordination.WaitContenderReady();
        grant.Revoke();
        coordination.SignalOwnerRevoked();
        coordination.WaitContenderDone();
    }

    [Theory(Explicit = true)]
    [InlineData(1u)]
    [Trait("CaseId", "G06-V1-SAME-ID-CONTENDER")]
    [Trait("Profile", "local")]
    [Trait("ExternalCoordination", "true")]
    public void G06V1ContenderRetainsItsRequestAfterOwnerRevokesTheSameId(uint requestId) {
        using var coordination = XenIfaceIoctlCoordination.Open("g06-v1");
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;

        coordination.WaitOwnerReady();
        using (var foreignRevoke = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS,
            new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN { RequestId = requestId })) {
            AssertRejected(foreignRevoke);
        }
        using var grant = PermitV1(handle, domain, requestId);
        coordination.SignalContenderReady();
        coordination.WaitOwnerRevoked();

        Assert.False(
            grant.Request.WaitTerminal(ObservationTimeout),
            "Owner revoke completed the contender's same-ID request");
        grant.Revoke();
        coordination.SignalContenderDone();
    }

    [Theory(Explicit = true)]
    [InlineData(0x10000000L)]
    [Trait("CaseId", "G06-V2-SAME-VA-OWNER")]
    [Trait("Profile", "local")]
    [Trait("ExternalCoordination", "true")]
    public void G06V2OwnerRetainsItsRequestWhenContenderUsesTheSameAddress(long addressValue) {
        using var coordination = XenIfaceIoctlCoordination.Open("g06-v2");
        var domain = fixture.RequireDomainId();
        var address = (nint)addressValue;
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var page = XenIfaceTestMemoryRegion.TryCreateAt(address);
        Assert.True(page is not null, $"Could not reserve G06 V2 address 0x{addressValue:X} in the owner process");
        using var grant = PermitV2(handle, domain, page!.Address);

        coordination.SignalOwnerReady();
        coordination.WaitContenderReady();
        grant.Revoke();
        coordination.SignalOwnerRevoked();
        coordination.WaitContenderDone();
    }

    [Theory(Explicit = true)]
    [InlineData(0x10000000L)]
    [Trait("CaseId", "G06-V2-SAME-VA-CONTENDER")]
    [Trait("Profile", "local")]
    [Trait("ExternalCoordination", "true")]
    public void G06V2ContenderRetainsItsRequestAfterOwnerRevokesTheSameAddress(long addressValue) {
        using var coordination = XenIfaceIoctlCoordination.Open("g06-v2");
        var domain = fixture.RequireDomainId();
        var address = (nint)addressValue;
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;

        coordination.WaitOwnerReady();
        using (var foreignRevoke = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2,
            new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2 { Address = address })) {
            AssertRejected(foreignRevoke);
        }
        using var page = XenIfaceTestMemoryRegion.TryCreateAt(address);
        Assert.True(page is not null, $"Could not reserve G06 V2 address 0x{addressValue:X} in the contender process");
        using var grant = PermitV2(handle, domain, page!.Address);
        coordination.SignalContenderReady();
        coordination.WaitOwnerRevoked();

        Assert.False(grant.Request.WaitTerminal(ObservationTimeout), "Owner revoke completed the contender's same-address request");
        grant.Revoke();
        coordination.SignalContenderDone();
    }
}
