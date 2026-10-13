using System.Runtime.InteropServices;
using static XenPlus.XENIFACE_GNTTAB_PAGE_FLAGS;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class GnttabIoctlTests {
    [Fact]
    [Trait("CaseId", "G16-PERMIT-PAGE-COUNT")]
    [Trait("Profile", "isolated")]
    public void G16PermitRejectsZeroAndOverflowPageCounts() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        var overflowPages = checked(uint.MaxValue / (uint)PageSize + 1u);

        foreach (var legacy in new[] { true, false }) {
            foreach (var pages in new[] { 0u, overflowPages, uint.MaxValue }) {
                // Submit forged metadata with a small real output buffer; never allocate gigabytes.
                using var invalid = BeginValidationPermit(handle, legacy, domain, memory.Address,
                    pages, XenIfaceIoctlAbi.PermitOutputLength(1));
                AssertRejected(invalid);
                AssertValidationGrantSmoke(handle, domain);
            }
        }
    }

    [Fact]
    [Trait("CaseId", "G16-PERMIT-OUTPUT-CAPACITY")]
    [Trait("Profile", "isolated")]
    public void G16PermitRejectsMorePagesThanFitInItsOutput() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted(pages: 2);
        var required = XenIfaceIoctlAbi.PermitOutputLength(2);

        foreach (var legacy in new[] { true, false }) {
            foreach (var outputLength in new[] {
                0,
                nint.Size - 1,
                XenIfaceIoctlAbi.PermitOutputLength(1),
                required - 1
            }) {
                using var invalid = BeginValidationPermit(handle, legacy, domain, memory.Address, 2, outputLength);
                AssertRejected(invalid);
                AssertValidationGrantSmoke(handle, domain);
            }
        }
    }

    [Fact]
    [Trait("CaseId", "G16-MAP-PAGE-COUNT")]
    [Trait("Profile", "isolated")]
    public void G16MapRejectsCountsLargerThanTheSuppliedReferenceArray() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        var maximumPages = uint.MaxValue / (uint)PageSize;

        foreach (var legacy in new[] { true, false }) {
            foreach (var pages in new[] { 0u, 2u, 17u, maximumPages, maximumPages + 1u, uint.MaxValue }) {
                // The declared count exceeds the actual request capacity.
                using var invalid = BeginValidationMap(handle, legacy, domain, pages, [uint.MaxValue]);
                AssertRejected(invalid);
                AssertValidationGrantSmoke(handle, domain);
            }
        }
    }

    [Fact]
    [Trait("CaseId", "G16-MAP-BUFFER-SIZES")]
    [Trait("Profile", "isolated")]
    public void G16MapRejectsTruncatedHeadersAndWrongOutputLengths() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;

        foreach (var legacy in new[] { true, false }) {
            var code = legacy ? IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES : IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_V2;
            var referencesOffset = legacy ? XenIfaceIoctlAbi.MapV1ReferencesOffset : XenIfaceIoctlAbi.MapV2ReferencesOffset;
            foreach (var inputLength in new[] {
                0,
                referencesOffset - 1,
                referencesOffset,
                referencesOffset + sizeof(uint) - 1
            }) {
                // No header initializer: writing a whole struct into these deliberately short
                // allocations would corrupt the test process before the driver sees the request.
                using var invalid = handle.Ioctl(code, inputLength, nint.Size);
                AssertRejected(invalid);
                AssertValidationGrantSmoke(handle, domain);
            }
            foreach (var outputLength in new[] { 0, nint.Size - 1, nint.Size + 1 }) {
                using var invalid = BeginValidationMap(
                    handle,
                    legacy,
                    domain,
                    1,
                    [uint.MaxValue],
                    outputLength: outputLength);
                AssertRejected(invalid);
                AssertValidationGrantSmoke(handle, domain);
            }
        }
    }

    [Fact]
    [Trait("CaseId", "G16-MAP-REFERENCE-SIZE-LIMIT")]
    [Trait("Profile", "isolated")]
    public void G16MapRejectsAnOversizedReferenceTailEvenWithASmallDeclaredCount() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        var maximumReferenceBytes = checked((int)(uint.MaxValue / (uint)PageSize) * sizeof(uint));

        foreach (var legacy in new[] { true, false }) {
            // Allocate about 4 MiB of actual request storage, never 4 GiB of guest pages.
            using var invalid = BeginValidationMap(handle, legacy, domain, 1, [uint.MaxValue],
                trailingBytes: maximumReferenceBytes + 1 - sizeof(uint));
            AssertRejected(invalid);
            AssertValidationGrantSmoke(handle, domain);
        }
    }

    [Fact]
    [Trait("CaseId", "G16-NOTIFY-OFFSET-DECLARED-SIZE")]
    [Trait("Profile", "isolated")]
    public void G16NotifyOffsetIsBoundedByDeclaredPagesRatherThanBufferCapacity() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted(pages: 2);
        var extraReferences = Enumerable.Repeat(uint.MaxValue, 17).ToArray();

        foreach (var legacy in new[] { true, false }) {
            foreach (var offset in new[] { (uint)PageSize, (uint)PageSize + 1u, uint.MaxValue }) {
                using (var permit = BeginValidationPermit(handle, legacy, domain, memory.Address,
                    1, XenIfaceIoctlAbi.PermitOutputLength(17), XENIFACE_GNTTAB_USE_NOTIFY_OFFSET, offset)) {
                    AssertRejected(permit);
                }
                using (var map = BeginValidationMap(handle, legacy, domain, 1, extraReferences,
                    XENIFACE_GNTTAB_USE_NOTIFY_OFFSET, offset)) {
                    AssertRejected(map);
                }
                AssertValidationGrantSmoke(handle, domain);
            }
        }
    }

    [Fact]
    [Trait("CaseId", "G16-PERMIT-OUTPUT-PADDING")]
    [Trait("Profile", "local")]
    public unsafe void G16PermitWritesOnlyItsDeclaredReferencesInALargerOutput() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        var outputLength = XenIfaceIoctlAbi.PermitOutputLength(17);

        foreach (var legacy in new[] { true, false }) {
            using var grant = CreateValidationGrant(handle, legacy, domain, memory.Address, outputLength,
                writeOutput: buffer => new Span<byte>((void*)buffer, outputLength).Fill(0xA5));
            Assert.NotEqual(0, grant.Request.ReadInt32(XenIfaceIoctlAbi.PermitReferencesOffset));
            for (var offset = XenIfaceIoctlAbi.PermitOutputLength(1); offset < outputLength; offset += sizeof(uint)) {
                Assert.Equal(unchecked((int)0xA5A5A5A5), grant.Request.ReadInt32(offset));
            }
            grant.Revoke();
            AssertValidationGrantSmoke(handle, domain);
        }
    }

    [Fact]
    [Trait("CaseId", "G16-PERMIT-IGNORED-NOTIFY-FIELDS")]
    [Trait("Profile", "isolated")]
    public void G16PermitIgnoresBogusNotifyFieldsWhenTheirFlagsAreClear() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        memory.WriteByte(127, 0xA6);
        memory.WriteByte(PageSize - 1, 0x73);

        foreach (var legacy in new[] { true, false }) {
            using var grant = CreateValidationGrant(handle, legacy, domain, memory.Address,
                XenIfaceIoctlAbi.PermitOutputLength(1), notifyOffset: uint.MaxValue, notifyPort: uint.MaxValue);
            grant.Revoke();
            Assert.Equal((byte)0xA6, memory.ReadByte(127));
            Assert.Equal((byte)0x73, memory.ReadByte(PageSize - 1));
            AssertValidationGrantSmoke(handle, domain);
        }
    }

    [Fact]
    [Trait("CaseId", "G16-PERMIT-NOTIFY-PORT")]
    [Trait("Profile", "isolated")]
    public void G16PermitCleanupCompletesWithUnknownOrClosedNotifyPorts() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        using var memory = XenIfaceTestMemoryRegion.AllocateCommitted();
        var closedPort = CreateValidationClosedPort(handle, domain);

        foreach (var legacy in new[] { true, false }) {
            foreach (var port in new[] { 0u, uint.MaxValue, closedPort }) {
                foreach (var cancel in new[] { false, true }) {
                    memory.WriteByte(126, 0x39);
                    memory.WriteByte(127, 0xA6);
                    memory.WriteByte(128, 0x73);
                    using var grant = CreateValidationGrant(handle, legacy, domain, memory.Address,
                        XenIfaceIoctlAbi.PermitOutputLength(1),
                        XENIFACE_GNTTAB_USE_NOTIFY_OFFSET | XENIFACE_GNTTAB_USE_NOTIFY_PORT, 127, port);
                    // Missing ports are nonfatal at cleanup; they are not submission errors.
                    if (cancel) {
                        CancelValidationRequest(grant.Request);
                    } else {
                        grant.Revoke();
                    }
                    if (!legacy) {
                        Assert.Equal((byte)0x39, memory.ReadByte(126));
                        Assert.Equal((byte)0, memory.ReadByte(127));
                        Assert.Equal((byte)0x73, memory.ReadByte(128));
                    }
                    AssertValidationGrantSmoke(handle, domain);
                }
            }
        }
    }

    [Fact]
    [Trait("CaseId", "G16-MAP-FORGED-REFERENCES")]
    [Trait("Profile", "isolated")]
    public void G16MapRejectsForgedReferencesAndDomainsWithoutStrandingValidGrants() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;
        foreach (var legacy in new[] { true, false }) {
            // Every reference is invalid: never install a same-domain grant mapping.
            foreach (var invalidReferences in new uint[][] { [uint.MaxValue], [uint.MaxValue, uint.MaxValue] }) {
                using (var invalid = BeginValidationMap(
                    handle,
                    legacy,
                    domain,
                    (uint)invalidReferences.Length,
                    invalidReferences)) {
                    AssertRejected(invalid);
                }
                AssertValidationGrantSmoke(handle, domain);
            }
            using (var invalidDomain = BeginValidationMap(handle, legacy, ushort.MaxValue, 1, [uint.MaxValue])) {
                AssertRejected(invalidDomain);
            }
            AssertValidationGrantSmoke(handle, domain);
        }
    }

    static PendingIoctl BeginValidationPermit(XenIfaceIoctlHandle handle, bool legacy, ushort domain,
        nint address, uint pages, int outputLength, XENIFACE_GNTTAB_PAGE_FLAGS flags = 0,
        uint notifyOffset = 0, uint notifyPort = 0, uint requestId = 0, Action<nint>? writeOutput = null) {
        return legacy
            ? Ioctl(handle, IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS,
                new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN {
                    RequestId = requestId,
                    RemoteDomain = domain,
                    NumberPages = pages,
                    Flags = flags,
                    NotifyOffset = notifyOffset,
                    NotifyPort = notifyPort
                }, outputLength, writeOutput)
            : Ioctl(handle, IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2,
                new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2 {
                    RemoteDomain = domain,
                    Address = address,
                    NumberPages = pages,
                    Flags = flags,
                    NotifyOffset = notifyOffset,
                    NotifyPort = notifyPort
                }, outputLength, writeOutput);
    }

    static XenIfaceTestGrant CreateValidationGrant(XenIfaceIoctlHandle handle, bool legacy, ushort domain,
        nint address, int outputLength, XENIFACE_GNTTAB_PAGE_FLAGS flags = 0,
        uint notifyOffset = 0, uint notifyPort = 0, Action<nint>? writeOutput = null) {
        var requestId = XenIfaceIoctlFixture.NewRequestId();
        var request = BeginValidationPermit(handle, legacy, domain, address, 1, outputLength,
            flags, notifyOffset, notifyPort, requestId, writeOutput);
        if (legacy) {
            return XenIfaceTestGrant.CreateV1(handle, requestId, request);
        } else {
            return XenIfaceTestGrant.CreateV2(handle, request);
        }
    }

    static unsafe PendingIoctl BeginValidationMap(
        XenIfaceIoctlHandle handle,
        bool legacy,
        ushort domain,
        uint pages,
        uint[] references,
        XENIFACE_GNTTAB_PAGE_FLAGS flags = 0,
        uint notifyOffset = 0,
        uint notifyPort = 0,
        uint requestId = 0,
        int trailingBytes = 0,
        int? outputLength = null) {
        var referencesOffset = legacy ? XenIfaceIoctlAbi.MapV1ReferencesOffset : XenIfaceIoctlAbi.MapV2ReferencesOffset;
        var inputLength = checked(referencesOffset + references.Length * sizeof(uint) + trailingBytes);
        return handle.Ioctl(
            legacy ? IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES : IOCTL_XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_V2,
            inputLength, outputLength ?? nint.Size, buffer => {
                if (legacy) {
                    Marshal.StructureToPtr(new XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_IN {
                        RequestId = requestId,
                        RemoteDomain = domain,
                        NumberPages = pages,
                        Flags = flags,
                        NotifyOffset = notifyOffset,
                        NotifyPort = notifyPort
                    }, buffer, false);
                } else {
                    Marshal.StructureToPtr(
                        new XENIFACE_GNTTAB_MAP_FOREIGN_PAGES_IN_V2 {
                            RemoteDomain = domain,
                            NumberPages = pages,
                            Flags = flags,
                            NotifyOffset = notifyOffset,
                            NotifyPort = notifyPort
                        },
                        buffer,
                        false);
                }
                for (var index = 0; index < references.Length; index++) {
                    Marshal.WriteInt32(
                        buffer,
                        referencesOffset + index * sizeof(uint),
                        unchecked((int)references[index]));
                }
                // Poison partial trailing elements too; they must not become grant references.
                var tailOffset = referencesOffset + references.Length * sizeof(uint);
                new Span<byte>((void*)(buffer + tailOffset), inputLength - tailOffset).Fill(0xFF);
            });
    }

    static void AssertValidationGrantSmoke(XenIfaceIoctlHandle handle, ushort domain) {
        using var grant = PermitV2(handle, domain, 0);
        grant.Revoke();
    }

    static void CancelValidationRequest(PendingIoctl request) {
        Assert.True(request.CancelAndWait(CompletionTimeout), "Cancel did not reach terminal completion");
        Assert.False(request.TerminalSuccess, "Canceled request unexpectedly completed successfully");
    }

    static uint CreateValidationClosedPort(XenIfaceIoctlHandle handle, ushort domain) {
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var bind = Ioctl(handle, IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 1,
                Event = notification.SafeWaitHandle.DangerousGetHandle()
            }, sizeof(uint));
        AssertSucceeded(bind);
        using var channel = new XenIfaceTestEventChannel(handle, unchecked((uint)bind.ReadInt32()));
        channel.Close();
        return channel.Port;
    }
}
