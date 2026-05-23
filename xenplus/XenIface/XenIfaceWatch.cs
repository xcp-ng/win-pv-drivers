namespace XenPlus.XenIface;

/// <summary>
/// An abstract watch bound to a <c>XenIface</c> (in contrast to <c>WatchAbiHandle</c> which is bound to a specific
/// <c>XenIfaceDevice</c>).
/// </summary>
public abstract class XenIfaceWatch : IDisposable {
    public abstract void Dispose();
    public abstract string Path { get; }
    internal abstract void Rearm(XenIfaceDevice device);
}

sealed class XenIfaceWatchImpl : XenIfaceWatch {
    readonly string _path;
    readonly XenIfaceSource _iface;
    readonly EventWaitHandle _event;
    readonly RegisteredWaitHandle _eventWait;
    bool _eventWaitRegistered = false;
    WatchAbiHandle? _watchHandle = null;
    bool _disposed = false;

    public override string Path => _path;

    internal XenIfaceWatchImpl(string path, XenIfaceSource iface, EventWaitHandle evt, XenIfaceDevice device) {
        try {
            _path = path;
            _iface = iface;
            _event = evt;
            _eventWait = ThreadPool.RegisterWaitForSingleObject(_event, (state, _) => {
                WatchTriggered?.Invoke(this, new XenIfaceWatchEventArgs(path));
            }, this, -1, false);
            _eventWaitRegistered = true;
            Rearm(device);
        } catch {
            Dispose();
            throw;
        }
    }

    public delegate void XenIfaceWatchEventHandler(object sender, XenIfaceWatchEventArgs args);
    public event XenIfaceWatchEventHandler? WatchTriggered;

    internal override void Rearm(XenIfaceDevice device) {
        _watchHandle?.Dispose();
        _watchHandle = null;
        _watchHandle = device.WatchAdd(_path, _event.SafeWaitHandle);
    }

    public override void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }

        _iface.WatchUnregister(this);

        _watchHandle?.Dispose();
        _watchHandle = null;

        if (_eventWaitRegistered) {
            _eventWait.Unregister(null);
            _eventWaitRegistered = false;
        }
        _event.Dispose();
    }
}
