namespace XenPlus;

public class ReferenceCountTests {
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task WaitCompletesWithoutReferences() {
        var references = new ReferenceCount();

        await references.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WaitCompletesAfterLastReferenceIsReleased() {
        var references = new ReferenceCount();
        Assert.True(references.TryAcquire());
        Assert.True(references.TryAcquire());

        var wait = references.WaitAsync(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
        Assert.False(wait.IsCompleted);

        references.Release();
        Assert.False(wait.IsCompleted);

        references.Release();
        await wait.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WaitCanTimeOutWhileReferenceIsHeld() {
        var references = new ReferenceCount();
        Assert.True(references.TryAcquire());

        await Assert.ThrowsAsync<TimeoutException>(
            () => references.WaitAsync(TimeSpan.Zero, TestContext.Current.CancellationToken));

        references.Release();
    }

    [Fact]
    public async Task BeginRundownRejectsNewReferencesAndDrainsExistingOnes() {
        var references = new ReferenceCount();
        Assert.True(references.TryAcquire());

        references.BeginRundown();

        Assert.False(references.TryAcquire());
        Assert.Null(references.TryEnterScope());

        var wait = references.WaitAsync(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
        Assert.False(wait.IsCompleted);

        references.Release();
        await wait.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CountedReferenceReleasesExactlyOnce() {
        var references = new ReferenceCount();
        var scope = Assert.IsType<CountedReference>(references.TryEnterScope());

        var wait = references.WaitAsync(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
        Assert.False(wait.IsCompleted);

        scope.Dispose();
        scope.Dispose();

        await wait.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RundownWaitsForExistingReferences() {
        var references = new ReferenceCount();
        var scope = Assert.IsType<CountedReference>(references.TryEnterScope());

        var rundown = references.RundownAsync(
            Timeout.InfiniteTimeSpan,
            TestContext.Current.CancellationToken);
        Assert.False(rundown.IsCompleted);
        Assert.Null(references.TryEnterScope());

        scope.Dispose();
        await rundown.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }
}
