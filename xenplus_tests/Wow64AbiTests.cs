using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

[Trait("Category", "XenIfaceIoctl")]
[Collection("XenIfaceIoctl")]
public sealed class Wow64AbiTests(XenIfaceIoctlFixture fixture) : IClassFixture<XenIfaceIoctlFixture> {
    [Fact]
    [Trait("CaseId", "ABI-WOW64-EVTCHN")]
    [Trait("Profile", "wow64")]
    public void Wow64EventChannelBindUsesTheNativeWindowsX64DriverContract() {
        RequireWow64();
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var notification = new EventWaitHandle(false, EventResetMode.ManualReset);

        using var bind = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = 0,
                Event = notification.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        var bound = bind.ImmediateSuccess;
        if (bound) {
            using var channel = new XenIfaceTestEventChannel(handle, unchecked((uint)bind.ReadInt32()));
            Assert.True(channel.Close(), "Bind succeeded but close failed");
        }

        Assert.True(bound, $"A valid native x86 event-channel bind was rejected with Win32 error {bind.ImmediateError}; the x64 driver does not translate this WOW64 ABI");
    }

    [Fact]
    [Trait("CaseId", "ABI-WOW64-GNTTAB-V1")]
    [Trait("Profile", "wow64")]
    public void Wow64V1GrantUsesTheNativeWindowsX64DriverContract() {
        RequireWow64();
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        var requestId = XenIfaceIoctlFixture.NewRequestId();
        using var permit = Ioctl(
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
            XenIfaceIoctlAbi.PermitOutputLength(1));

        var granted = permit.IsPending;
        if (granted) {
            using var revoke = Ioctl(
                handle,
                IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS,
                new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN { RequestId = requestId });
            Assert.True(revoke.ImmediateSuccess, $"Permit succeeded but revoke failed with {revoke.ImmediateError}");
            Assert.True(permit.WaitTerminal(CompletionTimeout), "The V1 permit request did not complete after revoke");
            Assert.True(permit.TerminalSuccess, $"V1 permit ended with {permit.TerminalError}");
        }

        Assert.True(granted, $"A valid native x86 V1 grant request was rejected with Win32 error {permit.ImmediateError}; its reference-array output layout is incompatible with the x64 driver");
    }

    [Fact]
    [Trait("CaseId", "ABI-WOW64-GNTTAB-V2")]
    [Trait("Profile", "wow64")]
    public void Wow64V2GrantUsesTheNativeWindowsX64DriverContract() {
        RequireWow64();
        var domain = fixture.RequireDomainId();
        using var handle = fixture.OpenDevice();
        using var permit = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_V2,
            new XENIFACE_GNTTAB_PERMIT_FOREIGN_ACCESS_IN_V2 {
                RemoteDomain = domain,
                Address = 0,
                NumberPages = 1,
                Flags = 0,
                NotifyOffset = 0,
                NotifyPort = 0
            },
            XenIfaceIoctlAbi.PermitOutputLength(1));

        var granted = permit.IsPending;
        if (granted) {
            using var revoke = Ioctl(
                handle,
                IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2,
                new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2 { Address = permit.ReadIntPtr() });
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
