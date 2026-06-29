namespace XenPlus;

public sealed class AsyncMonitor {
    sealed class Waiter {
        public readonly TaskCompletionSource Source = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    readonly SemaphoreSlim _lock = new(1, 1);

    readonly Lock _waitersLock = new();
    /// <summary>
    /// locked by <see cref="_waitersLock"/>
    /// </summary>
    readonly LinkedList<Waiter> _waiters = [];

    public async Task<AsyncMonitorScope> EnterScopeAsync(CancellationToken cancellationToken = default) {
        await _lock.WaitAsync(cancellationToken);
        return new(this);
    }

    void Release() {
        _lock.Release();
    }

    /// <summary>
    /// Timeouts and cancellations return/throw with the monitor reacquired.
    /// </summary>
    async Task ReacquireAsync(AsyncMonitorScope scope) {
        await _lock.WaitAsync(CancellationToken.None);
        try {
            scope.EndWait();
        } catch {
            _lock.Release();
            throw;
        }
    }

    /// <returns>
    /// <see langword="true"/> if waiter is still queued, <see langword="false"/> if waiter has already been pulsed.
    /// </returns>
    bool CancelWait(LinkedListNode<Waiter> node) {
        lock (_waitersLock) {
            if (node.List == null) {
                return false;
            }

            Check.DebugAssert(ReferenceEquals(node.List, _waiters));
            _waiters.Remove(node);
            return true;
        }
    }

    async Task<bool> WaitAsync(
        AsyncMonitorScope scope,
        TimeSpan timeout,
        CancellationToken cancellationToken) {

        if (timeout != Timeout.InfiniteTimeSpan && timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        scope.Validate();

        var waiter = new Waiter();
        LinkedListNode<Waiter> node;
        lock (_waitersLock) {
            node = _waiters.AddLast(waiter);
        }

        scope.BeginWait();

        var result = true;
        Exception? exception = null;

        try {
            await waiter.Source.Task.WaitAsync(timeout, cancellationToken);
        } catch (TimeoutException) {
            // if cannot cancel, then the waiter has already been pulsed, so we must complete
            result = !CancelWait(node);
        } catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested) {
            // same as above, if waiter pulsed then we must swallow exception and complete
            if (CancelWait(node)) {
                exception = ex;
            }
        } finally {
            await ReacquireAsync(scope);
        }

        if (exception != null) {
            throw exception;
        }

        return result;
    }

    void Pulse() {
        Waiter? waiter = null;

        lock (_waitersLock) {
            var node = _waiters.First;
            if (node != null) {
                waiter = node.Value;
                _waiters.Remove(node);
            }
        }

        waiter?.Source.TrySetResult();
    }

    void PulseAll() {
        List<Waiter> waiters = [];

        lock (_waitersLock) {
            while (_waiters.Count != 0) {
                var node = _waiters.First;
                if (node != null) {
                    waiters.Add(node.Value);
                    _waiters.RemoveFirst();
                }
            }
        }

        foreach (var waiter in waiters) {
            waiter.Source.TrySetResult();
        }
    }

    public sealed class AsyncMonitorScope(AsyncMonitor _monitor) : IDisposable {
        bool _ownsLock = true;
        bool _disposed = false;

        internal void Validate() {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_ownsLock) {
                throw new InvalidOperationException("monitor scope is not owned");
            }
        }

        internal void BeginWait() {
            Validate();
            _ownsLock = false;
            _monitor.Release();
        }

        internal void EndWait() {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Check.DebugAssert(!_ownsLock);
            _ownsLock = true;
        }

        /// <returns>
        /// <see langword="true"/> if pulsed; <see langword="false"/> if the timeout elapsed.
        /// </returns>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default) {
            return _monitor.WaitAsync(this, timeout, cancellationToken);
        }

        public void Pulse() {
            Validate();
            _monitor.Pulse();
        }

        public void PulseAll() {
            Validate();
            _monitor.PulseAll();
        }

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, true)) {
                return;
            }

            if (_ownsLock) {
                _ownsLock = false;
                _monitor.Release();
            }
        }
    }
}
