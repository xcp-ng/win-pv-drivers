using System.Runtime.CompilerServices;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

[Trait("Category", "XenIfaceIoctl")]
[Collection("XenIfaceIoctl")]
public sealed class Wow64AbiTests(XenIfaceIoctlFixture fixture) : IClassFixture<XenIfaceIoctlFixture> {
    [Fact]
    [Trait("CaseId", "ABI-WOW64-EVTCHN")]
    [Trait("Profile", "wow64")]
    public unsafe void Wow64EventChannelBindUsesTheNativeWindowsX64DriverContract() {
        RequireWow64();
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);

        using var bind = handle.Ioctl(
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            Unsafe.SizeOf<XENIFACE_EVTCHN_BIND_UNBOUND_IN>(),
            sizeof(uint),
            p => *(XENIFACE_EVTCHN_BIND_UNBOUND_IN*)p = new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = notification.SafeWaitHandle.DangerousGetHandle()
            });
        var bound = bind.ImmediateSuccess;
        uint port = 0;
        if (bound) {
            port = unchecked((uint)bind.ReadInt32());
            using var close = handle.Ioctl(IOCTL_XENIFACE_EVTCHN_CLOSE, Unsafe.SizeOf<XENIFACE_EVTCHN_CLOSE_IN>(), 0,
                p => *(XENIFACE_EVTCHN_CLOSE_IN*)p = new XENIFACE_EVTCHN_CLOSE_IN { LocalPort = port });
            Assert.False(close.IsPending, "WOW64 EVTCHN_CLOSE unexpectedly returned STATUS_PENDING");
            Assert.True(close.ImmediateSuccess, $"Bind succeeded but close failed with {close.ImmediateError}");
        }

        Assert.True(bound, $"A valid native x86 event-channel bind was rejected with Win32 error {bind.ImmediateError}; the x64 driver does not translate this WOW64 ABI");
    }

    [Fact]
    [Trait("CaseId", "ABI-WOW64-GNTTAB-V1")]
    [Trait("Profile", "wow64")]
    public unsafe void Wow64V1GrantUsesTheNativeWindowsX64DriverContract() {
        RequireWow64();
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        var requestId = XenIfaceIoctlFixture.NewRequestId();
        using var permit = handle.Ioctl(
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS,
            Unsafe.SizeOf<XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN>(),
            nint.Size + sizeof(uint),
            p => *(XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN*)p = new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN {
                RequestId = requestId,
                RemoteDomain = domain,
                NumberPages = 1,
                Flags = 0,
                NotifyOffset = 0,
                NotifyPort = 0
            });

        var granted = permit.IsPending;
        if (granted) {
            using var revoke = handle.Ioctl(
                IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS,
                Unsafe.SizeOf<XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN>(),
                0,
                p => *(XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN*)p = new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN { RequestId = requestId });
            Assert.True(revoke.ImmediateSuccess, $"Permit succeeded but revoke failed with {revoke.ImmediateError}");
            Assert.True(permit.WaitTerminal(CompletionTimeout), "The V1 permit request did not complete after revoke");
            Assert.True(permit.TerminalSuccess, $"V1 permit ended with {permit.TerminalError}");
        }

        Assert.True(granted, $"A valid native x86 V1 grant request was rejected with Win32 error {permit.ImmediateError}; its reference-array output layout is incompatible with the x64 driver");
    }

    [Fact]
    [Trait("CaseId", "ABI-WOW64-GNTTAB-V2")]
    [Trait("Profile", "wow64")]
    public unsafe void Wow64V2GrantUsesTheNativeWindowsX64DriverContract() {
        RequireWow64();
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var permit = handle.Ioctl(
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2,
            Unsafe.SizeOf<XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2>(),
            nint.Size + sizeof(uint),
            p => *(XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2*)p = new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2 {
                RemoteDomain = domain,
                Address = 0,
                NumberPages = 1,
                Flags = 0,
                NotifyOffset = 0,
                NotifyPort = 0
            });

        var granted = permit.IsPending;
        if (granted) {
            var address = permit.ReadIntPtr();
            using var revoke = handle.Ioctl(
                IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2,
                Unsafe.SizeOf<XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2>(),
                0,
                p => *(XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2*)p = new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2 { Address = address });
            Assert.True(revoke.ImmediateSuccess, $"Permit succeeded but V2 revoke failed with {revoke.ImmediateError}");
            Assert.True(permit.WaitTerminal(CompletionTimeout), "The V2 permit request did not complete after revoke");
            Assert.True(permit.TerminalSuccess, $"V2 permit ended with {permit.TerminalError}");
        }

        Assert.True(granted, $"A valid native x86 V2 grant request was rejected with Win32 error {permit.ImmediateError}; its pointer fields are incompatible with the x64 driver");
    }

    static void RequireWow64() {
        if (Environment.Is64BitProcess || !Environment.Is64BitOperatingSystem) {
            Assert.Skip("The WOW64 compatibility cases require a 32-bit process on a 64-bit Windows operating system");
        }
    }
}
