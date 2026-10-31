using System.Diagnostics;
using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

/// <summary>
/// Guest side of the XenStore protocol shared with <c>xi-gntmap.py --xenstore</c>. The guest writes
/// <c>refs</c>, <c>pages</c> and <c>guest-state</c>; the dom0 partner writes <c>partner-state</c>.
/// All keys live under <c>data/xeniface-test/TOKEN/</c> relative to the guest's domain home.
/// </summary>
sealed class XenIfaceRemotePartner : IDisposable {
    static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(60);
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    const int ValueBufferLength = 256;

    readonly XenIfaceIoctlHandle _handle;
    readonly string _basePath;
    bool _partnerSeen;
    bool _finished;

    XenIfaceRemotePartner(XenIfaceIoctlHandle handle, string token) {
        _handle = handle;
        _basePath = $"data/xeniface-test/{token}/";
    }

    /// <summary>Clears stale keys from an earlier run with the same token and announces the guest.</summary>
    public static XenIfaceRemotePartner Open(XenIfaceIoctlHandle handle, string token) {
        var partner = new XenIfaceRemotePartner(handle, token);
        partner.Remove("partner-state");
        partner.Remove("partner-domid");
        partner.Remove("refs");
        partner.Remove("pages");
        partner.Write("guest-state", "starting");
        return partner;
    }

    public void PublishGrant(IEnumerable<uint> references, int pages) {
        Write("refs", string.Join(",", references));
        Write("pages", pages.ToString());
        Publish("granted");
    }

    public void Publish(string guestState) {
        Write("guest-state", guestState);
        Console.WriteLine($"COORDINATION guest-state={guestState}");
        Console.Out.Flush();
    }

    /// <summary>Waits up to 60 seconds for <c>partner-state</c> to equal <paramref name="expected"/>.</summary>
    public void WaitPartnerState(string expected) {
        var timer = Stopwatch.StartNew();
        string? last = null;
        while (true) {
            last = Read("partner-state");
            if (last is not null) {
                _partnerSeen = true;
                Assert.False(last.StartsWith("error:"), $"Partner reported '{last}' while waiting for partner-state={expected}");
                if (last == expected) {
                    Console.WriteLine($"COORDINATION observed partner-state={expected}");
                    Console.Out.Flush();
                    return;
                }
            }
            if (timer.Elapsed >= StateTimeout) {
                break;
            }
            Thread.Sleep(PollInterval);
        }
        Assert.Fail($"Timed out after {StateTimeout.TotalSeconds:0} s waiting for partner-state={expected}; last seen '{last ?? "<none>"}'");
    }

    /// <summary>Tells the partner to unmap and waits for it to confirm.</summary>
    public void Finish() {
        _finished = true;
        Publish("done");
        WaitPartnerState("unmapped");
    }

    string KeyPath(string key) {
        return _basePath + key;
    }

    string? Read(string key) {
        using var request = StoreIoctl(_handle, IOCTL_XENIFACE_STORE_READ, KeyPath(key) + "\0", ValueBufferLength);
        if (request.IsPending && !request.WaitTerminal(CompletionTimeout)) {
            return null;
        }
        if (!request.TerminalSuccess) {
            return null;
        }
        return request.ReadAsciiString(ValueBufferLength);
    }

    void Write(string key, string value) {
        // Input: NUL-terminated path, NUL-terminated value, final NUL terminator.
        using var request = StoreIoctl(_handle, IOCTL_XENIFACE_STORE_WRITE, KeyPath(key) + "\0" + value + "\0\0", 0);
        AssertSucceeded(request);
    }

    /// <summary>Best effort; a missing key or a refusal is not an error.</summary>
    void Remove(string key) {
        using var request = StoreIoctl(_handle, IOCTL_XENIFACE_STORE_REMOVE, KeyPath(key) + "\0", 0);
        if (request.IsPending) {
            request.WaitTerminal(CompletionTimeout);
        }
    }

    /// <summary>
    /// Always releases the partner. When the case did not finish normally, wait for the unmap before the
    /// caller frees the granted memory, but never throw from cleanup.
    /// </summary>
    public void Dispose() {
        if (_finished) {
            return;
        }
        _finished = true;
        try {
            Publish("done");
            if (_partnerSeen) {
                WaitPartnerState("unmapped");
            }
        } catch (Exception error) {
            Console.WriteLine($"COORDINATION cleanup: {error.Message}");
        }
    }
}
