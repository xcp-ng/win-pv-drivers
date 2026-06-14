namespace XenPlus.XenIface;

/// <summary>
/// An abstract watch bound to a <see cref="XenIfaceSource"/> (in contrast to <see cref="WatchAbiHandle"/> which is
/// bound to a specific <see cref="XenIfaceDevice"/>
/// </summary>
abstract class XenIfaceWatch : IDisposable {
    public abstract void Dispose();
    public abstract string Path { get; }
    internal abstract void Rearm(XenIfaceDevice device);

    public delegate void XenIfaceWatchEventHandler(object? sender, XenIfaceWatchEventArgs args);
    public abstract event XenIfaceWatchEventHandler? WatchTriggered;

    /// <summary>
    /// One-shot wait.
    /// </summary>
    /// <param name="timeoutMilliseconds">Timeout, in milliseconds</param>
    /// <param name="discount">Number of waits to ignore before completing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>the watched path</returns>
    public abstract Task<string?> WaitOneAsync(
        int timeoutMilliseconds,
        int discount = 0,
        CancellationToken cancellationToken = default);
}

sealed class XenIfaceWatchImpl : XenIfaceWatch {
    readonly string _path;
    readonly bool _strict;
    readonly XenIfaceSource _iface;
    readonly EventWaitHandle _event;
    readonly RegisteredWaitHandle _eventWait;
    bool _eventWaitRegistered = false;
    /// <summary>
    /// interlocked
    /// </summary>
    bool _watchFiredFirstTime = false;
    WatchAbiHandle? _watchHandle = null;
    bool _disposed = false;
    readonly TaskCompletionSource _disposedSource = new();

    public override event XenIfaceWatchEventHandler? WatchTriggered;

    public override string Path => _path;

    internal XenIfaceWatchImpl(
        string path,
        bool strict,
        XenIfaceSource iface,
        EventWaitHandle evt,
        XenIfaceDevice? device) {

        try {
            _path = path;
            _strict = strict;
            _iface = iface;
            _event = evt;
            _eventWait = ThreadPool.RegisterWaitForSingleObject(_event, (state, _) => {
                if (Interlocked.Exchange(ref _watchFiredFirstTime, true)) {
                    WatchTriggered?.Invoke(this, new XenIfaceWatchEventArgs());
                }
            }, this, Timeout.Infinite, false);
            _eventWaitRegistered = true;
            Rearm(device);
        } catch {
            Dispose();
            throw;
        }
    }

    internal override void Rearm(XenIfaceDevice? device) {
        _watchHandle?.Dispose();
        // the purpose of this is to be exception-safe wrt. WatchAdd
        _watchHandle = null;
        Interlocked.Exchange(ref _watchFiredFirstTime, false);
        if (device == null) {
            return;
        }
        _watchHandle = device.WatchAdd(_path, _event.SafeWaitHandle, _strict);
    }

    public override async Task<string?> WaitOneAsync(
        int timeoutMilliseconds,
        int discount = 0,
        CancellationToken cancellationToken = default) {

        var source = new TaskCompletionSource();
        var remainingDiscount = discount + 1;
        void handler(object? sender, XenIfaceWatchEventArgs args) {
            if (Interlocked.Decrement(ref remainingDiscount) == 0) {
                source.TrySetResult();
            }
        }
        WatchTriggered += handler;
        try {
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            var completed = await Task.WhenAny(
                source.Task,
                timeoutTask,
                _disposedSource.Task);
            ObjectDisposedException.ThrowIf(completed == _disposedSource.Task, this);
            if (completed == timeoutTask) {
                throw new TimeoutException();
            }
            Utils.DebugAssert(completed == source.Task);
            return _path;
        } finally {
            WatchTriggered -= handler;
            source.TrySetCanceled(CancellationToken.None);
        }
    }

    public override void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }

        _disposedSource.SetCanceled();

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
