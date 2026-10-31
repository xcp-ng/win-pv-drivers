using static XenPlus.XenIfaceIoctl;
using static XenPlus.XenIfaceIoctlTestSupport;

namespace XenPlus;

sealed class XenIfaceTestEventChannel : IDisposable {
    readonly XenIfaceIoctlHandle _handle;
    bool _closed;

    public XenIfaceTestEventChannel(XenIfaceIoctlHandle handle, uint port) {
        Assert.NotEqual(0u, port);
        _handle = handle;
        Port = port;
    }

    public uint Port { get; }

    public static XenIfaceTestEventChannel BindUnbound(
        XenIfaceIoctlHandle handle, ushort domain, EventWaitHandle evt, byte mask = 0) {
        using var bind = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_BIND_UNBOUND,
            new XENIFACE_EVTCHN_BIND_UNBOUND_IN {
                RemoteDomain = domain,
                Mask = mask,
                Event = evt.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bind);
        return new(handle, unchecked((uint)bind.ReadInt32()));
    }

    public static XenIfaceTestEventChannel BindInterdomain(
        XenIfaceIoctlHandle handle, ushort domain, uint remotePort, EventWaitHandle evt, byte mask = 0) {
        using var bind = Ioctl(
            handle,
            IOCTL_XENIFACE_EVTCHN_BIND_INTERDOMAIN,
            new XENIFACE_EVTCHN_BIND_INTERDOMAIN_IN {
                RemoteDomain = domain,
                RemotePort = remotePort,
                Mask = mask,
                Event = evt.SafeWaitHandle.DangerousGetHandle()
            },
            sizeof(uint));
        AssertSucceeded(bind);
        return new(handle, unchecked((uint)bind.ReadInt32()));
    }

    public void Notify() {
        using var notify = Ioctl(
            _handle,
            IOCTL_XENIFACE_EVTCHN_NOTIFY,
            new XENIFACE_EVTCHN_NOTIFY_IN { LocalPort = Port });
        AssertSucceeded(notify);
    }

    public void Unmask() {
        using var unmask = Ioctl(
            _handle,
            IOCTL_XENIFACE_EVTCHN_UNMASK,
            new XENIFACE_EVTCHN_UNMASK_IN { LocalPort = Port });
        AssertSucceeded(unmask);
    }

    public bool Close() {
        if (_closed) {
            return true;
        }

        using var close = Ioctl(
            _handle,
            IOCTL_XENIFACE_EVTCHN_CLOSE,
            new XENIFACE_EVTCHN_CLOSE_IN {
                LocalPort = Port
            });
        _closed = close.ImmediateSuccess;
        return _closed;
    }

    public void Dispose() {
        Close();
    }
}

/// <summary>Two same-domain channels, A unbound on <c>a</c> and B bound to it on <c>b</c>. Does not own the events.</summary>
sealed class XenIfaceTestEventChannelPair : IDisposable {
    readonly EventWaitHandle _eventA;
    readonly EventWaitHandle _eventB;

    public XenIfaceTestEventChannelPair(
        XenIfaceIoctlHandle a,
        XenIfaceIoctlHandle b,
        ushort domain,
        EventWaitHandle eventA,
        EventWaitHandle eventB,
        byte maskB = 0) {
        _eventA = eventA;
        _eventB = eventB;
        A = XenIfaceTestEventChannel.BindUnbound(a, domain, eventA);
        try {
            B = XenIfaceTestEventChannel.BindInterdomain(b, domain, A.Port, eventB, maskB);
        } catch {
            A.Dispose();
            throw;
        }
    }

    public XenIfaceTestEventChannel A { get; }
    public XenIfaceTestEventChannel B { get; }

    /// <summary>A notifies B, then B notifies A; each side must observe it. Discards bind-time signals first.</summary>
    public void Handshake() {
        _eventA.Reset();
        _eventB.Reset();
        A.Notify();
        Assert.True(_eventB.WaitOne(CompletionTimeout), "Endpoint B did not observe A's notify");
        _eventB.Reset();
        B.Notify();
        Assert.True(_eventA.WaitOne(CompletionTimeout), "Endpoint A did not observe B's notify");
    }

    public void Dispose() {
        B.Dispose();
        A.Dispose();
    }
}
