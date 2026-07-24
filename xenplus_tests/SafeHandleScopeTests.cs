using System.Runtime.InteropServices;

namespace XenPlus;

public class SafeHandleScopeTests {
    sealed class TrackingSafeHandle : SafeHandle {
        int _releaseCount;

        public int ReleaseCount => Volatile.Read(ref _releaseCount);

        public TrackingSafeHandle(nint value) : base(nint.Zero, true) {
            SetHandle(value);
        }

        public override bool IsInvalid => handle == nint.Zero;

        protected override bool ReleaseHandle() {
            Interlocked.Increment(ref _releaseCount);
            handle = nint.Zero;
            return true;
        }
    }

    [Fact]
    public void SemaphoreScopeReleasesExactlyOnce() {
        using var semaphore = new SemaphoreSlim(1, 1);
        var scope = semaphore.EnterScope();

        Assert.Equal(0, semaphore.CurrentCount);

        scope.Dispose();
        scope.Dispose();
        Assert.Equal(1, semaphore.CurrentCount);
    }

    [Fact]
    public async Task SemaphoreScopeSerializesAsyncAccess() {
        using var semaphore = new SemaphoreSlim(1, 1);
        var first = semaphore.EnterScope();

        var secondTask = semaphore.EnterScopeAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(secondTask.IsCompleted);

        first.Dispose();
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(0, semaphore.CurrentCount);
    }

    [Fact]
    public async Task CancelledSemaphoreEntryDoesNotReleaseSemaphore() {
        using var semaphore = new SemaphoreSlim(1, 1);
        using var first = semaphore.EnterScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => semaphore.EnterScopeAsync(cancellation.Token));

        Assert.Equal(0, semaphore.CurrentCount);
    }

    [Fact]
    public void SafeHandleBorrowDefersRelease() {
        var handle = new TrackingSafeHandle(123);
        var borrowed = handle.Borrow();

        handle.Dispose();

        Assert.Equal(0, handle.ReleaseCount);
        Assert.Equal(123, borrowed.DangerousHandle);

        borrowed.Dispose();
        borrowed.Dispose();
        Assert.Equal(1, handle.ReleaseCount);
    }

    [Fact]
    public void SafeHandleBorrowRejectsClosedHandle() {
        var handle = new TrackingSafeHandle(123);
        handle.Dispose();

        Assert.Throws<ObjectDisposedException>(handle.Borrow);
        Assert.Equal(1, handle.ReleaseCount);
    }
}
