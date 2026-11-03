using Windows.Win32.System.Memory;
using static XenPlus.XENIFACE_GNTTAB_PAGE_FLAGS;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class GnttabIoctlTests {
    [Fact]
    [Trait("CaseId", "G15-READWRITE-ON-READONLY")]
    [Trait("Profile", "isolated")]
    public void G15WritableGrantRejectsCommittedPageReadOnlyMemory() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        memory.WriteByte(0, 0x6B);
        memory.Protect(PAGE_PROTECTION_FLAGS.PAGE_READONLY);
        Assert.Equal(PAGE_PROTECTION_FLAGS.PAGE_READONLY, memory.QueryProtection());

        AssertRejectedWithSmoke(handle, domain, BeginPermitV2(handle, domain, memory.Address));
        Assert.Equal(PAGE_PROTECTION_FLAGS.PAGE_READONLY, memory.QueryProtection());
        Assert.Equal((byte)0x6B, memory.ReadByte(0));
    }

    [Fact]
    [Trait("CaseId", "G15-RESERVED-CALLER-MEMORY")]
    [Trait("Profile", "isolated")]
    public void G15GrantRejectsReservedButUncommittedMemory() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.Reserve();
        Assert.Equal(VIRTUAL_ALLOCATION_TYPE.MEM_RESERVE, memory.QueryState());

        AssertRejectedWithSmoke(handle, domain, BeginPermitV2(handle, domain, memory.Address));
    }

    [Fact]
    [Trait("CaseId", "G15-UNMAPPED-CALLER-MEMORY")]
    [Trait("Profile", "isolated")]
    public void G15GrantRejectsAnUnmappedCallerAddress() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        // Another thread (e.g. the JIT) can reserve the freed range before the driver probes it, which turns the
        // request into a valid grant of that thread's memory. Retry with a new address when that happened.
        for (var attempt = 1; ; attempt++) {
            using var memory = XenIfaceTestMemoryRegion.AllocateUnmapped();
            Assert.Equal(VIRTUAL_ALLOCATION_TYPE.MEM_FREE, memory.QueryState());
            var rejected = BeginPermitV2(handle, domain, memory.Address);
            if (attempt < 3 && memory.QueryState() != VIRTUAL_ALLOCATION_TYPE.MEM_FREE) {
                rejected.Dispose();
                continue;
            }
            AssertRejectedWithSmoke(handle, domain, rejected);
            return;
        }
    }

    [Fact]
    [Trait("CaseId", "G15-DECOMMITTED-CALLER-MEMORY")]
    [Trait("Profile", "isolated")]
    public void G15GrantRejectsCommittedRangeAfterPageWasDecommitted() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        memory.DecommitPage();
        Assert.Equal(VIRTUAL_ALLOCATION_TYPE.MEM_RESERVE, memory.QueryState());

        AssertRejectedWithSmoke(handle, domain, BeginPermitV2(handle, domain, memory.Address));
    }

    [Fact]
    [Trait("CaseId", "G15-NOACCESS-CALLER-MEMORY")]
    [Trait("Profile", "isolated")]
    public void G15GrantRejectsCommittedNoAccessMemory() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        memory.Protect(PAGE_PROTECTION_FLAGS.PAGE_NOACCESS);
        Assert.Equal(PAGE_PROTECTION_FLAGS.PAGE_NOACCESS, memory.QueryProtection());

        AssertRejectedWithSmoke(handle, domain, BeginPermitV2(handle, domain, memory.Address));
    }

    [Fact]
    [Trait("CaseId", "G15-GUARD-CALLER-MEMORY")]
    [Trait("Profile", "isolated")]
    public void G15GrantRejectsGuardPageMemory() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        memory.Protect(PAGE_PROTECTION_FLAGS.PAGE_READWRITE | PAGE_PROTECTION_FLAGS.PAGE_GUARD);
        Assert.Equal(PAGE_PROTECTION_FLAGS.PAGE_READWRITE | PAGE_PROTECTION_FLAGS.PAGE_GUARD, memory.QueryProtection());

        AssertRejectedWithSmoke(handle, domain, BeginPermitV2(handle, domain, memory.Address));
    }

    [Fact]
    [Trait("CaseId", "G15-CROSS-PAGE-INACCESSIBLE")]
    [Trait("Profile", "isolated")]
    public void G15GrantRejectsRangeWithSecondPageInaccessible() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted(pages: 2);
        memory.ProtectPage(1, PAGE_PROTECTION_FLAGS.PAGE_NOACCESS);
        Assert.Equal(PAGE_PROTECTION_FLAGS.PAGE_NOACCESS, memory.QueryProtection(offset: PageSize));

        AssertRejectedWithSmoke(handle, domain, BeginPermitV2(handle, domain, memory.Address, pages: 2));
    }

    [Fact]
    [Trait("CaseId", "G15-COMMITTED-DEMAND-ZERO")]
    [Trait("Profile", "local")]
    public void G15GrantFaultsInUntouchedCommittedDemandZeroMemory() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();

        using var grant = PermitV2(handle, domain, memory.Address);
        Assert.Equal((byte)0, memory.ReadByte(0));
        grant.Revoke();

        memory.WriteByte(0, 0x2E);
        Assert.Equal((byte)0x2E, memory.ReadByte(0));
    }

    [Fact]
    [Trait("CaseId", "G15-READONLY-GRANT-LOCAL")]
    [Trait("Profile", "local")]
    public void G15ReadonlyGrantAcceptsCommittedPageReadOnlyMemory() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        memory.WriteByte(0, 0x5D);
        memory.Protect(PAGE_PROTECTION_FLAGS.PAGE_READONLY);
        Assert.Equal(PAGE_PROTECTION_FLAGS.PAGE_READONLY, memory.QueryProtection());

        using var grant = PermitV2(handle, domain, memory.Address, XENIFACE_GNTTAB_READONLY);
        Assert.Equal((byte)0x5D, memory.ReadByte(0));
        grant.Revoke();
        Assert.Equal(PAGE_PROTECTION_FLAGS.PAGE_READONLY, memory.QueryProtection());
        Assert.Equal((byte)0x5D, memory.ReadByte(0));
    }

    [Fact]
    [Trait("CaseId", "G15-TRIMMED-COMMITTED-PAGE")]
    [Trait("Profile", "isolated")]
    public void G15GrantFaultsInCommittedPageTrimmedOutsideTheWorkingSet() {
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        memory.WriteByte(0, 0x73);
        if (!memory.TrimOutsideWorkingSet()) {
            Assert.Skip("The OS did not trim the target page outside this process working set");
        }
        using var grant = PermitV2(handle, domain, memory.Address);
        Assert.Equal((byte)0x73, memory.ReadByte(0));
    }

    static void AssertRejectedWithSmoke(XenIfaceIoctlHandle handle, ushort domain, PendingIoctl rejected) {
        using (rejected) {
            AssertRejected(rejected);
        }
        using var smoke = PermitV2(handle, domain, 0);
    }
}
