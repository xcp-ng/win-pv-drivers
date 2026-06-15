namespace XenPlus;

sealed class ReferenceCount {
    readonly Lock _lock = new();
    TaskCompletionSource _tcs;
    int _count = 0;

    public ReferenceCount() {
        _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _tcs.SetResult();
    }

    public void Acquire() {
        lock (_lock) {
            if (_count == 0) {
                _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            _count++;
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

    public CountedReference EnterScope() {
        return new(this);
    }
}

sealed class CountedReference : IDisposable {
    readonly ReferenceCount _parent;
    bool _acquired = false;

    public CountedReference(ReferenceCount parent) {
        _parent = parent;
        _parent.Acquire();
        _acquired = true;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _acquired, false) == false) {
            return;
        }
        _parent.Release();
    }
}
