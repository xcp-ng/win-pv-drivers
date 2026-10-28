using static XenPlus.XENIFACE_GNTTAB_PAGE_FLAGS;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

[Trait("Category", "XenIfaceIoctl")]
[Collection("XenIfaceIoctl")]
public sealed partial class GnttabIoctlTests(XenIfaceIoctlFixture fixture) : IClassFixture<XenIfaceIoctlFixture> {
    static void RevokeV1(XenIfaceIoctlHandle handle, uint requestId, int outputLength = 0) {
        using var revoke = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS,
            new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN {
                RequestId = requestId
            },
            outputLength);
        AssertSucceeded(revoke);
    }

    static XenIfaceTestGrant PermitV1(
        XenIfaceIoctlHandle handle,
        ushort domain,
        uint requestId,
        XENIFACE_GNTTAB_PAGE_FLAGS flags = 0,
        uint notifyOffset = 0,
        uint notifyPort = 0,
        uint pages = 1) {
        var outputLength = XenIfaceIoctlAbi.PermitOutputLength(pages);
        var pending = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS,
            new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN {
                RequestId = requestId,
                RemoteDomain = domain,
                NumberPages = pages,
                Flags = flags,
                NotifyOffset = notifyOffset,
                NotifyPort = notifyPort
            },
            outputLength);
        return XenIfaceTestGrant.CreateV1(handle, requestId, pending);
    }

    static XenIfaceTestGrant PermitV2(
        XenIfaceIoctlHandle handle,
        ushort domain,
        nint address,
        XENIFACE_GNTTAB_PAGE_FLAGS flags = 0,
        uint notifyOffset = 0,
        uint notifyPort = 0,
        uint pages = 1) {
        var pending = BeginPermitV2(handle, domain, address, flags, notifyOffset, notifyPort, pages);
        return XenIfaceTestGrant.CreateV2(handle, pending);
    }

    static PendingIoctl BeginPermitV2(
        XenIfaceIoctlHandle handle,
        ushort domain,
        nint address,
        XENIFACE_GNTTAB_PAGE_FLAGS flags = 0,
        uint notifyOffset = 0,
        uint notifyPort = 0,
        uint pages = 1) {
        return Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2,
            new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2 {
                RemoteDomain = domain,
                Address = address,
                NumberPages = pages,
                Flags = flags,
                NotifyOffset = notifyOffset,
                NotifyPort = notifyPort,
            },
            XenIfaceIoctlAbi.PermitOutputLength(pages));
    }

    [Fact]
    [Trait("CaseId", "G01-V1-PERMIT-REVOKE")]
    [Trait("Profile", "local")]
    public void G01V1PermitAndRevokeLeavesTheRequestUsable() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        var requestId = XenIfaceIoctlFixture.NewRequestId();
        using var grant = PermitV1(handle, domain, requestId);
        Assert.NotEqual(0, grant.Request.ReadInt32(nint.Size));
        grant.Revoke();
    }

    [Fact]
    [Trait("CaseId", "G01-V2-ALLOCATED")]
    [Trait("Profile", "local")]
    public void G01V2DriverAllocatedPermitAndRevoke() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var grant = PermitV2(handle, domain, 0);
        grant.Revoke();
    }

    [Fact]
    [Trait("CaseId", "G01-V2-CALLER-OWNED")]
    [Trait("Profile", "local")]
    public void G01V2CallerOwnedPageRemainsUsableAfterRevoke() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var page = XenIfaceTestMemoryRegion.AllocateCommitted();
        page.WriteByte(0, 0x41);
        using var grant = PermitV2(handle, domain, page.Address);
        grant.Revoke();
        Assert.Equal(0x41, page.ReadByte(0));
        page.WriteByte(1, 0x7b);
        Assert.Equal(0x7b, page.ReadByte(1));
    }

    [Fact]
    [Trait("CaseId", "G04-NOTIFY-OFFSET")]
    [Trait("Profile", "local")]
    public void G04RevokeClearsOnlyTheSelectedByteInCallerMemory() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var page = XenIfaceTestMemoryRegion.AllocateCommitted();
        page.WriteByte(PageSize - 2, 0x56);
        page.WriteByte(PageSize - 1, 0x7a);
        using var grant = PermitV2(
            handle,
            domain,
            page.Address,
            XENIFACE_GNTTAB_USE_NOTIFY_OFFSET,
            checked((uint)(PageSize - 1)));
        grant.Revoke();
        Assert.Equal(0x56, page.ReadByte(PageSize - 2));
        Assert.Equal(0, page.ReadByte(PageSize - 1));
    }

    [Fact]
    [Trait("CaseId", "G05-OFFSET-BOUNDARY")]
    [Trait("Profile", "local")]
    public void G05NotifyOffsetAtAndBeyondRegionSizeIsRejected() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var page = XenIfaceTestMemoryRegion.AllocateCommitted();
        var pageSize = checked((uint)PageSize);
        foreach (var offset in new uint[] { pageSize, pageSize + 1u }) {
            using var invalid = Ioctl(
                handle,
                IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2,
                new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2 {
                    RemoteDomain = domain,
                    Address = page.Address,
                    NumberPages = 1,
                    Flags = XENIFACE_GNTTAB_USE_NOTIFY_OFFSET,
                    NotifyOffset = offset,
                    NotifyPort = 0,
                },
                XenIfaceIoctlAbi.PermitOutputLength(1));
            AssertRejected(invalid);
        }
    }

    [Fact]
    [Trait("CaseId", "G07-DUPLICATE-V1-ID")]
    [Trait("Profile", "local")]
    public void G07DuplicateV1RequestIdDoesNotReplaceTheOriginal() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        var requestId = XenIfaceIoctlFixture.NewRequestId();
        using var original = PermitV1(handle, domain, requestId);
        using (var duplicate = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS,
            new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN {
                RequestId = requestId,
                RemoteDomain = domain,
                NumberPages = 1,
                Flags = 0,
                NotifyOffset = 0,
                NotifyPort = 0
            },
            XenIfaceIoctlAbi.PermitOutputLength(1))) {
            AssertRejected(duplicate);
        }
        original.Revoke();
    }

    [Fact]
    [Trait("CaseId", "G08-DUPLICATE-ADDRESS")]
    [Trait("Profile", "local")]
    public void G08V2DuplicateCallerAddressDoesNotReplaceOriginalGrant() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var page = XenIfaceTestMemoryRegion.AllocateCommitted();
        using var original = PermitV2(handle, domain, page.Address);
        using (var duplicate = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2,
            new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2 {
                RemoteDomain = domain,
                Address = page.Address,
                NumberPages = 1,
                Flags = 0,
                NotifyOffset = 0,
                NotifyPort = 0
            },
            XenIfaceIoctlAbi.PermitOutputLength(1))) {
            AssertRejected(duplicate);
        }
        original.Revoke();
    }

    [Fact]
    [Trait("CaseId", "G09-REVOKE-OUTPUT-LENGTH")]
    [Trait("Profile", "local")]
    public void G09LegacyRevokeIgnoresOutputLengthAsImplemented() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        var requestId = XenIfaceIoctlFixture.NewRequestId();
        using var grant = PermitV1(handle, domain, requestId);
        // The current legacy revoke contract ignores OutLen; keep the buffer allocated and observe it.
        RevokeV1(handle, requestId, sizeof(uint));
        Assert.True(grant.Request.WaitTerminal(CompletionTimeout));
        Assert.True(grant.Request.TerminalSuccess, $"Permit request ended with {grant.Request.TerminalError}");
    }

    [Fact]
    [Trait("CaseId", "G11-INVALID-ADDRESS")]
    [Trait("Profile", "isolated")]
    public void G11InvalidCallerAddressFailsWithoutStrandingAValidGrant() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using (var invalid = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2,
            new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2 {
                RemoteDomain = domain,
                Address = 1,
                NumberPages = 1,
                Flags = 0,
                NotifyOffset = 0,
                NotifyPort = 0
            },
            XenIfaceIoctlAbi.PermitOutputLength(1))) {
            AssertRejected(invalid);
        }
        using var smoke = PermitV2(handle, domain, 0);
    }

    [Fact]
    [Trait("CaseId", "G12-CANCEL-REQUEST")]
    [Trait("Profile", "isolated")]
    public void G12CancelPendingGrantCompletesAndLeavesDeviceUsable() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var pending = BeginPermitV2(handle, domain, 0);
        AssertPending(pending);
        Assert.NotEqual(0, pending.ReadIntPtr());
        Assert.True(pending.CancelAndWait(CompletionTimeout), "CancelIoEx did not reach terminal completion");
        Assert.False(pending.TerminalSuccess, "Canceled permit unexpectedly completed successfully");
        Assert.Equal(995, pending.TerminalError); // ERROR_OPERATION_ABORTED
        using var smoke = PermitV2(handle, domain, 0);
    }
}
