using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

sealed class XenIfaceTestGrant : IDisposable {
    readonly XenIfaceIoctlHandle _handle;
    readonly uint? _v1RequestId;
    bool _revoked;
    bool _disposed;

    XenIfaceTestGrant(XenIfaceIoctlHandle handle, PendingIoctl request, uint? v1RequestId, nint address) {
        _handle = handle;
        Request = request;
        _v1RequestId = v1RequestId;
        Address = address;
    }

    public PendingIoctl Request { get; }
    public nint Address { get; }

    public static XenIfaceTestGrant CreateV1(XenIfaceIoctlHandle handle, uint requestId, PendingIoctl request) {
        try {
            AssertPending(request);
            var address = request.ReadIntPtr();
            Assert.NotEqual(0, address);
            return new XenIfaceTestGrant(handle, request, requestId, address);
        } catch {
            request.Dispose();
            throw;
        }
    }

    public static XenIfaceTestGrant CreateV2(XenIfaceIoctlHandle handle, PendingIoctl request) {
        try {
            AssertPending(request);
            var address = request.ReadIntPtr();
            Assert.NotEqual(0, address);
            return new XenIfaceTestGrant(handle, request, null, address);
        } catch {
            request.Dispose();
            throw;
        }
    }

    PendingIoctl InternalRevoke() {
        if (_v1RequestId is uint requestId) {
            return Ioctl(
                _handle,
                IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS,
                new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN { RequestId = requestId });
        } else {
            return Ioctl(
                _handle,
                IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2,
                new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2 { Address = Address });
        }
    }

    public void Revoke() {
        if (_revoked || Request.IsTerminal) {
            return;
        }

        using var revoke = InternalRevoke();
        _revoked = true;
        Assert.True(Request.WaitTerminal(CompletionTimeout), "Grant request did not complete after revoke");
        Assert.True(Request.TerminalSuccess, $"Grant request ended with {Request.TerminalError}");
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        try {
            if (!_revoked && !Request.IsTerminal) {
                using var revoke = InternalRevoke();
                _revoked = true;
                Request.WaitTerminal(CompletionTimeout);
            }
        } finally {
            Request.Dispose();
        }
    }
}
