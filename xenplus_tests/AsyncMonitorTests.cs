namespace XenPlus;

public class AsyncMonitorTests {
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task EnterScopeSerializesAccess() {
        var monitor = new AsyncMonitor();

        // Acquire and hold the monitor with the first scope.
        var first = await monitor.EnterScopeAsync(TestContext.Current.CancellationToken);

        // A second acquisition must remain blocked while the first scope owns the monitor.
        var secondTask = monitor.EnterScopeAsync(TestContext.Current.CancellationToken);
        Assert.False(secondTask.IsCompleted);

        // Releasing the first scope must allow the second acquisition to complete.
        first.Dispose();
        using var second = await secondTask.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WaitReturnsFalseAfterTimeoutAndReacquiresScope() {
        var monitor = new AsyncMonitor();
        using var scope = await monitor.EnterScopeAsync(TestContext.Current.CancellationToken);

        // An immediate timeout must complete without a pulse and report false.
        Assert.False(await scope.WaitAsync(TimeSpan.Zero, TestContext.Current.CancellationToken));

        // Pulse requires ownership, so this also proves WaitAsync reacquired the scope before returning.
        scope.Pulse();
    }

    [Fact]
    public async Task WaitReturnsTrueAfterPulse() {
        var monitor = new AsyncMonitor();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Start a task that acquires the monitor and begins an infinite wait.
        var waiter = Task.Run(async () => {
            using var scope = await monitor.EnterScopeAsync();
            waiting.SetResult();
            return await scope.WaitAsync(Timeout.InfiniteTimeSpan);
        });

        // Wait until the task is about to wait, then acquire the monitor. This acquisition can only
        // complete after the waiter has queued itself and released the monitor.
        await waiting.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        using (var scope = await monitor.EnterScopeAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestTimeout, TestContext.Current.CancellationToken)) {
            // Wake the single queued waiter.
            scope.Pulse();
        }

        // A waiter awakened by Pulse must report true.
        Assert.True(await waiter.WaitAsync(TestTimeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PulseAllWakesEveryWaiter() {
        var monitor = new AsyncMonitor();

        static async Task<bool> Wait(
            AsyncMonitor monitor,
            TaskCompletionSource waiting) {
            using var scope = await monitor.EnterScopeAsync();
            waiting.SetResult();
            return await scope.WaitAsync(Timeout.InfiniteTimeSpan);
        }

        // Queue the first waiter.
        var firstWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(() => Wait(monitor, firstWaiting));
        await firstWaiting.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        // Acquiring the monitor proves that the first waiter has queued itself and released the monitor.
        using (await monitor.EnterScopeAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestTimeout, TestContext.Current.CancellationToken)) {
        }

        // Queue the second waiter after the first, preserving a known queue order.
        var secondWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = Task.Run(() => Wait(monitor, secondWaiting));
        await secondWaiting.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        // Acquiring the monitor proves that the second waiter is also queued, then PulseAll wakes both.
        using (var scope = await monitor.EnterScopeAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestTimeout, TestContext.Current.CancellationToken)) {
            scope.PulseAll();
        }

        // Every waiter awakened by PulseAll must report true.
        Assert.True(await first.WaitAsync(TestTimeout, TestContext.Current.CancellationToken));
        Assert.True(await second.WaitAsync(TestTimeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationReacquiresScopeBeforeThrowing() {
        var monitor = new AsyncMonitor();
        using var scope = await monitor.EnterScopeAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Waiting with an already-cancelled token must propagate cancellation.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scope.WaitAsync(Timeout.InfiniteTimeSpan, cts.Token));

        // Pulse requires ownership, so this proves the scope was reacquired before cancellation escaped.
        scope.Pulse();
    }

    [Fact]
    public async Task WaitRejectsInvalidTimeout() {
        var monitor = new AsyncMonitor();
        using var scope = await monitor.EnterScopeAsync(TestContext.Current.CancellationToken);

        // Only non-negative timeouts and Timeout.InfiniteTimeSpan are accepted.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => scope.WaitAsync(TimeSpan.FromMilliseconds(-2), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposedScopeRejectsOperations() {
        var monitor = new AsyncMonitor();
        var scope = await monitor.EnterScopeAsync(TestContext.Current.CancellationToken);

        // Disposing a scope releases the monitor, and repeated disposal must be harmless.
        scope.Dispose();
        scope.Dispose();

        // A disposed scope can no longer wait or signal monitor waiters.
        Assert.Throws<ObjectDisposedException>(scope.Pulse);
        Assert.Throws<ObjectDisposedException>(scope.PulseAll);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => scope.WaitAsync(TimeSpan.Zero, TestContext.Current.CancellationToken));
    }
}
