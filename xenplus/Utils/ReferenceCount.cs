namespace XenPlus;

sealed class ReferenceCount {
    readonly Lock _lock = new();
    TaskCompletionSource _tcs;
    int _count = 0;
    bool _rundown = false;

    public ReferenceCount() {
        _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _tcs.SetResult();
    }

    public bool TryAcquire() {
        lock (_lock) {
            if (_rundown) {
                return false;
            }
            if (_count == 0) {
                _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            _count++;
            return true;
        }
    }

    public void Release() {
        lock (_lock) {
            Check.Assert(_count > 0, "reference count underflowed");
            if (--_count == 0) {
                _tcs.TrySetResult();
            }
        }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) {
        Task task;
        lock (_lock) {
            task = _tcs.Task;
        }
        await task.WaitAsync(timeout, cancellationToken);
    }

    public void BeginRundown() {
        lock (_lock) {
            _rundown = true;
            if (_count == 0) {
                _tcs.TrySetResult();
            }
        }
    }

    public async Task RundownAsync(TimeSpan timeout, CancellationToken cancellationToken) {
        BeginRundown();
        await WaitAsync(timeout, cancellationToken);
    }

    public CountedReference? TryEnterScope() {
        return TryAcquire() ? new(this, true) : null;
    }
}

sealed class CountedReference(ReferenceCount _parent, bool _acquired) : IDisposable {
    public void Dispose() {
        if (Interlocked.Exchange(ref _acquired, false) == false) {
            return;
        }
        _parent.Release();
    }
}
