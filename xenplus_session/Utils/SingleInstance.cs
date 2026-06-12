namespace XenPlus;

sealed class SingleInstance : IDisposable {
    readonly Mutex _mutex;
    bool _taken = false;
    bool _disposed = false;

    public bool IsTaken => _taken;

    public SingleInstance(string name, bool currentSessionOnly = true, bool currentUserOnly = true) {
        _mutex = new(name, new() {
            CurrentSessionOnly = currentSessionOnly,
            CurrentUserOnly = currentUserOnly,
        });
        try {
            _taken = _mutex.WaitOne(0);
        } catch (AbandonedMutexException) {
            _taken = true;
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }

        if (_taken) {
            _mutex.ReleaseMutex();
            _taken = false;
        }
        _mutex.Dispose();
    }
}
