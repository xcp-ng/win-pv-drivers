namespace XenPlus.XenIface;

// Given that watch/suspend handles are dependent on their device handles, it's simpler to just dictate that they have
// deterministic lifetimes instead of trying to destroy them unsafely using finalizers.

sealed class WatchAbiHandle(XenIfaceDevice? device, nint context) : IDisposable {
    public void Dispose() {
        var last = Interlocked.Exchange(ref context, nint.Zero);
        if (last != nint.Zero) {
            device?.WatchRemove(last);
            device = null;
        }
    }
}

sealed class SuspendAbiHandle(XenIfaceDevice? device, nint context) : IDisposable {
    public void Dispose() {
        var last = Interlocked.Exchange(ref context, nint.Zero);
        if (last != nint.Zero) {
            device?.SuspendDeregister(last);
            device = null;
        }
    }
}
