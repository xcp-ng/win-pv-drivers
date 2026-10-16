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
