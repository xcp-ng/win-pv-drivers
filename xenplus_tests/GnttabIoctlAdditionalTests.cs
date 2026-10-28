using static XenPlus.XENIFACE_GNTTAB_PAGE_FLAGS;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

public sealed partial class GnttabIoctlTests {
    [Fact]
    [Trait("CaseId", "G07-V1-ID0-V2-COEXIST")]
    [Trait("Profile", "local")]
    public void G07V1RequestIdZeroCoexistsWithV2GrantInBothOrders() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;

        {
            using var v1First = PermitV1(handle, domain, 0);
            using var v2Second = PermitV2(handle, domain, 0);
            Assert.False(v1First.Request.WaitTerminal(ObservationTimeout), "V1 request ID 0 completed before cleanup");
            Assert.False(v2Second.Request.WaitTerminal(ObservationTimeout), "V2 grant completed before cleanup");

            v1First.Revoke();
            Assert.False(v2Second.Request.WaitTerminal(ObservationTimeout), "Revoking V1 ID 0 also completed the V2 grant");

            v2Second.Revoke();
        }

        {
            using var v2First = PermitV2(handle, domain, 0);
            using var v1Second = PermitV1(handle, domain, 0);
            Assert.False(v2First.Request.WaitTerminal(ObservationTimeout), "V2 grant completed before cleanup");
            Assert.False(v1Second.Request.WaitTerminal(ObservationTimeout), "V1 request ID 0 completed before cleanup");

            v1Second.Revoke();
            Assert.False(v2First.Request.WaitTerminal(ObservationTimeout), "Revoking V1 ID 0 also completed the V2 grant");

            v2First.Revoke();
        }
    }

    [Fact]
    [Trait("CaseId", "G04-REVOKE-NOTIFY-LOOPBACK")]
    [Trait("Profile", "loopback")]
    public void G04RevokeSetsByteAndSignalsPairedEventChannel() {
        var domain = fixture.RequireDomainId();
        var (path, handleA) = fixture.OpenDevice();
        using var a = handleA;
        using var b = XenIfaceIoctlHandle.Open(path);
        using var eventA = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var eventB = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var page = XenIfaceTestMemoryRegion.AllocateCommitted();
        const int offset = 127;
        // write a sequence and verify the contents below
        page.WriteByte(offset - 1, 0x39);
        page.WriteByte(offset, 0xA6);
        page.WriteByte(offset + 1, 0x73);
        using var pair = new XenIfaceTestEventChannelPair(a, b, domain, eventA, eventB);

        // The interdomain bind notifies B asynchronously; observe it before resetting for revoke.
        Assert.True(eventB.WaitOne(CompletionTimeout), "Bind-time notification on B was not delivered");
        eventA.Reset();
        eventB.Reset();

        using var grant = PermitV2(
            handleA,
            domain,
            page.Address,
            XENIFACE_GNTTAB_USE_NOTIFY_OFFSET | XENIFACE_GNTTAB_USE_NOTIFY_PORT,
            offset,
            pair.A.Port);

        Assert.False(eventB.WaitOne(ObservationTimeout), "The paired endpoint was signaled before revoke");
        grant.Revoke();

        Assert.Equal(0x39, page.ReadByte(offset - 1));
        Assert.Equal(0, page.ReadByte(offset));
        Assert.Equal(0x73, page.ReadByte(offset + 1));
        Assert.True(eventB.WaitOne(CompletionTimeout), "The paired event endpoint did not observe revoke notification");
    }

    [Fact]
    [Trait("CaseId", "G13-ISSUING-THREAD-EXIT")]
    [Trait("Profile", "isolated")]
    public void G13IssuingThreadExitCompletesPermitWithOperationAborted() {
        var domain = fixture.RequireDomainId();
        var (_, handle) = fixture.OpenDevice();
        using var handleLifetime = handle;

        var issuer = new G13Issuer(handle, domain);
        var issuingThread = new Thread(issuer.Issue) {
            IsBackground = true,
            Name = "xeniface-g13-issuer",
        };

        issuingThread.Start();
        Check.Assert(issuingThread.Join(CompletionTimeout), "Issuing thread did not exit after submitting the overlapped permit");

        Assert.Null(issuer.Error);
        using var pending = Assert.IsType<PendingIoctl>(issuer.Request);
        Assert.True(pending.WaitTerminal(CompletionTimeout), "Issuing-thread exit did not complete the permit within the bounded wait");
        Assert.False(pending.TerminalSuccess, "Issuing-thread exit left the V2 grant active instead of completing it with ERROR_OPERATION_ABORTED");
        Assert.Equal(995, pending.TerminalError); // ERROR_OPERATION_ABORTED after the issuing thread exits

        using var smoke = PermitV2(handle, domain, 0);
    }

    sealed class G13Issuer(XenIfaceIoctlHandle handle, ushort domain) {
        public PendingIoctl? Request;
        public Exception? Error;

        public void Issue() {
            try {
                var request = BeginPermitV2(handle, domain, 0);
                try {
                    AssertPending(request);
                    Assert.NotEqual(0, request.ReadIntPtr());
                    Request = request;
                } catch {
                    request.Dispose();
                    throw;
                }
            } catch (Exception error) {
                Error = error;
            }
        }
    }
}
