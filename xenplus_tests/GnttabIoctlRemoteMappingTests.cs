using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

/// <summary>
/// G17: the grant is revoked (or its permit request is cancelled) while dom0 still maps it read-write.
/// Per case, start the partner in dom0 first (it waits up to 240 s; the guest waits 60 s per partner state):
/// <c>python3 xi-gntmap.py --domid DOMID --vm-uuid UUID --xenstore --token TOKEN</c>.
/// Then in the guest set <c>XENIFACE_IOCTL_COORDINATION_NAME=TOKEN</c> and run
/// <c>xenplus_tests.exe -explicit only -trait CaseId=G17-V2-USER-BUFFER</c>.
/// The guest publishes the grant references, the partner maps them and
/// reports <c>mapped</c>, the guest ends the grant and publishes the result, the partner stamps
/// <c>XIUAF:TOKEN:</c> over every mapped page and reports <c>stamped</c>, and the guest observes its memory.
/// Stopping the partner early via <c>RUN_DIR/stop</c> leaves that file behind; delete it before the next run.
/// </summary>
public sealed partial class GnttabIoctlTests {
    const int RemotePages = 4;
    const ushort Dom0 = 0;
    const int ScanChunkBytes = 64 * 1024 * 1024;
    const long ScanLimitBytes = 2L * 1024 * 1024 * 1024;
    const int StampRecordBytes = 64; // The partner repeats one 64-byte record through each page.
    static readonly TimeSpan RemoteScanDuration = TimeSpan.FromSeconds(20);

    [Fact(Explicit = true)]
    [Trait("CaseId", "G17-V2-USER-BUFFER")]
    [Trait("Profile", "manual")]
    [Trait("ExternalCoordination", "true")]
    public void G17V2RevokeWhileRemoteMapsCallerBufferDoesNotLetRemoteWritesLand() {
        var token = XenIfaceIoctlCoordination.RequireToken();
        using var handle = fixture.OpenDevice();
        using var buffer = XenIfaceTestMemoryRegion.AllocateCommitted(RemotePages);
        WriteGuestMarkers(buffer.Address, token);
        using var grant = PermitV2(handle, Dom0, buffer.Address, pages: RemotePages);
        using var partner = XenIfaceRemotePartner.Open(handle, token);

        partner.PublishGrant(ReadPermitReferences(grant), RemotePages);
        partner.WaitPartnerState("mapped");
        var revokeError = RevokeV2(handle, grant.Address);
        partner.Publish($"revoked:win32={revokeError}");
        partner.WaitPartnerState("stamped");
        var stampedPages = CountStampedPages(buffer.Address, token);
        partner.Finish();
        if (revokeError != 0) {
            // The grant stayed in place; once dom0 has unmapped it must be revocable.
            grant.Revoke();
        }

        Assert.False(
            revokeError == 0 && stampedPages > 0,
            "revoke reported success while dom0 still mapped the grant; remote writes landed in caller memory after " +
            $"the revoke ({stampedPages} of {RemotePages} pages carry XIUAF:{token}:)");
    }

    [Fact(Explicit = true)]
    [Trait("CaseId", "G17-V2-DRIVER-ALLOCATED")]
    [Trait("Profile", "manual")]
    [Trait("ExternalCoordination", "true")]
    public void G17V2RevokeWhileRemoteMapsDriverPagesFails() {
        var token = XenIfaceIoctlCoordination.RequireToken();
        using var handle = fixture.OpenDevice();
        using var grant = PermitV2(handle, Dom0, 0, pages: RemotePages);
        using var partner = XenIfaceRemotePartner.Open(handle, token);
        WriteGuestMarkers(grant.Address, token);

        partner.PublishGrant(ReadPermitReferences(grant), RemotePages);
        partner.WaitPartnerState("mapped");
        var revokeError = RevokeV2(handle, grant.Address);
        partner.Publish($"revoked:win32={revokeError}");
        partner.WaitPartnerState("stamped");
        // After a successful revoke the driver-owned user mapping is gone; do not touch grant.Address again.
        if (revokeError == 0) {
            ScanFreedPhysicalMemory(token);
        }
        partner.Finish();
        if (revokeError != 0) {
            grant.Revoke();
        }

        Assert.False(
            revokeError == 0,
            "revoke reported success while dom0 still held a live writable mapping of the driver-allocated pages " +
            "(partner-state=stamped); the driver must fail the revoke and keep the pages while they are mapped");
    }

    [Fact(Explicit = true)]
    [Trait("CaseId", "G17-V2-CANCEL")]
    [Trait("Profile", "manual")]
    [Trait("ExternalCoordination", "true")]
    public void G17V2CancelWhileRemoteMapsCallerBufferDoesNotLetRemoteWritesLand() {
        var token = XenIfaceIoctlCoordination.RequireToken();
        using var handle = fixture.OpenDevice();
        using var buffer = XenIfaceTestMemoryRegion.AllocateCommitted(RemotePages);
        WriteGuestMarkers(buffer.Address, token);
        using var grant = PermitV2(handle, Dom0, buffer.Address, pages: RemotePages);
        using var partner = XenIfaceRemotePartner.Open(handle, token);

        partner.PublishGrant(ReadPermitReferences(grant), RemotePages);
        partner.WaitPartnerState("mapped");
        // A completion held until dom0 unmaps is acceptable, so the cancel may not complete within the timeout.
        var completed = grant.Request.CancelAndWait(CompletionTimeout);
        partner.Publish(completed ? $"cancelled:win32={grant.Request.TerminalError}" : "cancelled:held");
        partner.WaitPartnerState("stamped");
        var stampedPages = CountStampedPages(buffer.Address, token);
        partner.Finish();
        Assert.True(grant.Request.WaitTerminal(CompletionTimeout), "Cancelled permit request did not complete after dom0 unmapped");

        Assert.False(
            completed && stampedPages > 0,
            "the permit IRP cancel completed while dom0 still mapped the grant; remote writes landed in caller " +
            $"memory after the cancel completed ({stampedPages} of {RemotePages} pages carry XIUAF:{token}:)");
    }

    /// <summary>Returns 0 when the revoke IOCTL succeeded, otherwise its Win32 error.</summary>
    static int RevokeV2(XenIfaceIoctlHandle handle, nint address) {
        using var revoke = Ioctl(
            handle,
            IOCTL_XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_V2,
            new XENIFACE_GNTTAB_REVOKE_FOREIGN_ACCESS_IN_V2 { Address = address });
        Assert.True(revoke.WaitTerminal(CompletionTimeout), "The revoke IOCTL did not complete");
        return revoke.TerminalSuccess ? 0 : revoke.TerminalError;
    }

    static List<uint> ReadPermitReferences(XenIfaceTestGrant grant) {
        var references = new List<uint>();
        for (var index = 0; index < RemotePages; index++) {
            references.Add(unchecked((uint)grant.Request.ReadInt32(
                XenIfaceIoctlAbi.PermitReferencesOffset + index * sizeof(uint))));
        }
        return references;
    }

    static unsafe void WriteGuestMarkers(nint address, string token) {
        for (var page = 0; page < RemotePages; page++) {
            var marker = Encoding.ASCII.GetBytes($"XIGUEST:{token}:p{page}");
            for (var index = 0; index < marker.Length; index++) {
                Volatile.Write(ref *(byte*)(address + page * PageSize + index), marker[index]);
            }
        }
    }

    static unsafe bool HasStampAt(nint address, byte[] prefix) {
        for (var index = 0; index < prefix.Length; index++) {
            if (Volatile.Read(ref *(byte*)(address + index)) != prefix[index]) {
                return false;
            }
        }
        return true;
    }

    static byte[] StampPrefix(string token) {
        return Encoding.ASCII.GetBytes($"XIUAF:{token}:");
    }

    static int CountStampedPages(nint address, string token) {
        var prefix = StampPrefix(token);
        var count = 0;
        for (var page = 0; page < RemotePages; page++) {
            if (HasStampAt(address + page * PageSize, prefix)) {
                count++;
            }
        }
        Report($"G17 buffer check: {count} of {RemotePages} pages carry the remote stamp");
        return count;
    }

    static void Report(string message) {
        Console.WriteLine(message);
        Console.Out.Flush();
        TestContext.Current.TestOutputHelper?.WriteLine(message);
    }

    static unsafe long FreePhysicalBytes() {
        var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        if (!PInvokeT.GlobalMemoryStatusEx(&status)) {
            return 0;
        }
        return checked((long)status.ullAvailPhys);
    }

    /// <summary>
    /// Informational: if the freed driver pages were reused, the partner's stamps may show up in memory we are
    /// handed. Never asserted.
    /// </summary>
    static void ScanFreedPhysicalMemory(string token) {
        var target = Math.Min(FreePhysicalBytes() / 4 * 3, ScanLimitBytes);
        var chunks = new List<XenIfaceTestMemoryRegion>();
        try {
            for (long committed = 0; committed + ScanChunkBytes <= target; committed += ScanChunkBytes) {
                XenIfaceTestMemoryRegion chunk;
                try {
                    chunk = XenIfaceTestMemoryRegion.AllocateCommitted(ScanChunkBytes / PageSize);
                } catch (Win32Exception) {
                    break;
                }
                chunks.Add(chunk);
                for (var offset = 0; offset < chunk.Length; offset += PageSize) {
                    chunk.WriteByte(offset, 0x5a);
                }
            }
            Report($"G17 scan: committed {chunks.Count * (long)ScanChunkBytes / (1024 * 1024)} MiB of {target / (1024 * 1024)} MiB target");

            var prefix = StampPrefix(token);
            var hits = 0;
            var samples = new List<string>();
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < RemoteScanDuration && hits == 0) {
                foreach (var chunk in chunks) {
                    for (var offset = 0; offset < chunk.Length; offset += StampRecordBytes) {
                        if (chunk.ReadByte(offset) == prefix[0] && HasStampAt(chunk.Address + offset, prefix)) {
                            hits++;
                            if (samples.Count < 5) {
                                samples.Add($"0x{chunk.Address + offset:X}: {ReadPrintable(chunk.Address + offset, 48)}");
                            }
                        }
                    }
                }
            }
            Report($"G17 scan: {hits} stamp record(s) found in {timer.Elapsed.TotalSeconds:0.0} s");
            foreach (var sample in samples) {
                Report($"G17 scan sample {sample}");
            }
        } finally {
            foreach (var chunk in chunks) {
                chunk.Dispose();
            }
        }
    }

    static unsafe string ReadPrintable(nint address, int length) {
        var text = new StringBuilder(length);
        for (var index = 0; index < length; index++) {
            var value = Volatile.Read(ref *(byte*)(address + index));
            text.Append(value is >= 32 and < 127 ? (char)value : '.');
        }
        return text.ToString();
    }
}
