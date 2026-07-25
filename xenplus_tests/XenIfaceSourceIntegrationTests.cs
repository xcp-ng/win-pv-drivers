using System.Diagnostics;
using XenPlus.XenIface;

namespace XenPlus;

[Trait("Category", "XenIface")]
public class XenIfaceSourceIntegrationTests(XenIfaceSourceFixture fixture) : IClassFixture<XenIfaceSourceFixture> {
    static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);
    static readonly TimeSpan DeviceRemovalBlockedTimeout = TimeSpan.FromSeconds(1);
    static readonly TimeSpan WatchReadyPollInterval = TimeSpan.FromMilliseconds(10);

    static async Task WaitForWatchReadyAsync(XenIfaceWatch watch, CancellationToken cancellationToken) {
        var deadline = Stopwatch.GetTimestamp() + (long)(EventTimeout.TotalSeconds * Stopwatch.Frequency);
        while (!watch.Ready) {
            if (Stopwatch.GetTimestamp() >= deadline) {
                throw new TimeoutException("The watch did not consume its initial notification");
            }
            await Task.Delay(WatchReadyPollInterval, cancellationToken);
        }
    }

    static void UseCopiedDisposedHandle(XenIfaceSource source) {
        var h = source.Lock();
        var copy = h;
        h.Dispose();

        copy.StoreTryRead("data");
    }

    [Fact]
    public async Task StoreRoundTripUsesActiveDevice() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        const string expected = "xenplus integration test";

        using var h = fixture.Source.Lock();
        h.StoreWriteStrict(storePath.Path, expected);

        Assert.Equal(expected, h.StoreReadStrict(storePath.Path));
    }

    [Fact]
    public async Task MissingStorePathsUseTryAndRequiredContracts() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();

        // Try variants report a missing path without throwing.
        using (var h = fixture.Source.Lock()) {
            Assert.Null(h.StoreTryRead(storePath.Path));
            Assert.Null(h.StoreTryReadStrict(storePath.Path));
            Assert.Null(h.StoreTryDirectory(storePath.Path));
        }

        // Required variants identify the missing XenStore path in FileName.
        var readException = Assert.Throws<FileNotFoundException>(() => {
            using var h = fixture.Source.Lock();
            h.StoreReadStrict(storePath.Path);
        });
        Assert.Equal(storePath.Path, readException.FileName);

        var directoryException = Assert.Throws<FileNotFoundException>(() => {
            using var h = fixture.Source.Lock();
            h.StoreDirectory(storePath.Path);
        });
        Assert.Equal(storePath.Path, directoryException.FileName);
    }

    [Fact]
    public async Task StoreDirectoryDistinguishesNodesLeavesAndMissingPaths() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        var leaf = $"{storePath.Path}/alpha";
        var branchLeaf = $"{storePath.Path}/beta/child";

        using var h = fixture.Source.Lock();
        h.StoreWriteStrict(leaf, "leaf");
        h.StoreWriteStrict(branchLeaf, "branch leaf");

        // A node lists its immediate children, a leaf has no children, and a missing path is null.
        Assert.Equal(
            ["alpha", "beta"],
            h.StoreDirectory(storePath.Path).Order(StringComparer.Ordinal));
        Assert.Empty(Assert.IsType<List<string>>(h.StoreTryDirectory(leaf)));
        Assert.Null(h.StoreTryDirectory($"{storePath.Path}/missing"));
    }

    [Fact]
    public async Task PermissiveStoreWriteReplacesInvalidCharacters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();

        using var h = fixture.Source.Lock();

        // The permissive API maps characters outside the XenStore character set to question marks.
        h.StoreWrite(storePath.Path, "\u001f\u0080");

        Assert.Equal("??", h.StoreReadStrict(storePath.Path));
    }

    [Fact]
    public async Task CopiedHandleBecomesUnusableAfterEitherCopyIsDisposed() {
        await fixture.RequireActiveDeviceAsync(TestContext.Current.CancellationToken);

        // Copies share the inner lock owner, so disposing either copy invalidates both.
        Assert.Throws<ObjectDisposedException>(() => UseCopiedDisposedHandle(fixture.Source));
    }

    [Fact]
    public async Task WatchTriggersForStoreChange() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        var triggered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var watch = fixture.Source.WatchAdd(storePath.Path);
        Assert.Equal(storePath.Path, watch.Path);
        await WaitForWatchReadyAsync(watch, cancellationToken);

        void OnWatchTriggered(object? sender, XenIfaceWatchEventArgs args) {
            triggered.TrySetResult(sender);
        }

        watch.WatchTriggered += OnWatchTriggered;
        // Changing the watched path must raise the watch event with the watch as sender.
        using (var h = fixture.Source.Lock()) {
            h.StoreWriteStrict(storePath.Path, "changed");
        }

        Assert.Same(watch, await triggered.Task.WaitAsync(EventTimeout, cancellationToken));
    }

    [Fact]
    public async Task DisableAndReenableDeviceRebindsSource() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var instanceId = await fixture.RequireActiveDeviceInstanceIdAsync(cancellationToken);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var awaitingRebind = false;

        void onResumed(object? sender, XenIfaceResumedEventArgs args) {
            if (Volatile.Read(ref awaitingRebind)) {
                resumed.TrySetResult();
            }
        }

        fixture.Source.Resumed += onResumed;
        try {
            try {
                await fixture.DisableDeviceAsync(instanceId, cancellationToken);
                Volatile.Write(ref awaitingRebind, true);
            } finally {
                // Recovery must not use the test cancellation token: always try to leave the device enabled.
                await fixture.EnableDeviceAsync(instanceId, cancellationToken);
            }

            // Reopening the interface registers the suspend event and raises Resumed through SuspendCallback.
            await resumed.Task.WaitAsync(XenIfaceSourceFixture.PnputilTimeout, cancellationToken);
            Assert.NotNull(fixture.Source.Active);
        } finally {
            fixture.Source.Resumed -= onResumed;
        }
    }

    [Fact]
    public async Task WatchIsRearmedAfterDisableAndReenable() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var instanceId = await fixture.RequireActiveDeviceInstanceIdAsync(cancellationToken);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watchTriggered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var awaitingRebind = false;
        var observeWatch = false;
        using var storePath = fixture.GetTempStorePath();

        void onResumed(object? sender, XenIfaceResumedEventArgs args) {
            if (Volatile.Read(ref awaitingRebind)) {
                resumed.TrySetResult();
            }
        }

        using var watch = fixture.Source.WatchAdd(storePath.Path);
        await WaitForWatchReadyAsync(watch, cancellationToken);

        void onWatchTriggered(object? sender, XenIfaceWatchEventArgs args) {
            if (Volatile.Read(ref observeWatch)) {
                watchTriggered.TrySetResult();
            }
        }

        fixture.Source.Resumed += onResumed;
        watch.WatchTriggered += onWatchTriggered;
        try {
            try {
                await fixture.DisableDeviceAsync(instanceId, cancellationToken);
                Volatile.Write(ref awaitingRebind, true);
            } finally {
                await fixture.EnableDeviceAsync(instanceId, cancellationToken);
            }

            // Resumed is raised only after SuspendCallback has rearmed every registered watch.
            await resumed.Task.WaitAsync(XenIfaceSourceFixture.PnputilTimeout, cancellationToken);
            await WaitForWatchReadyAsync(watch, cancellationToken);

            // Ignore registration/rearm signals and require a subsequent store change to trigger the watch.
            Volatile.Write(ref observeWatch, true);
            using (var h = fixture.Source.Lock()) {
                h.StoreWriteStrict(storePath.Path, "changed after rebind");
            }
            await watchTriggered.Task.WaitAsync(EventTimeout, cancellationToken);
        } finally {
            fixture.Source.Resumed -= onResumed;
            watch.WatchTriggered -= onWatchTriggered;
        }
    }

    [Fact]
    public async Task HandleDefersExternalDeviceDisableUntilDisposed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var instanceId = await fixture.RequireActiveDeviceInstanceIdAsync(cancellationToken);

        try {
            using var releaseHandle = new ManualResetEventSlim();
            var handleAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var holderTask = Task.Run(() => {
                using var h = fixture.Source.Lock();
                handleAcquired.TrySetResult();
                releaseHandle.Wait(cancellationToken);
            }, cancellationToken);

            Task disableTask;
            try {
                await handleAcquired.Task.WaitAsync(EventTimeout, cancellationToken);

                // RunPnputilAsync starts pnputil synchronously before returning its incomplete task.
                disableTask = fixture.DisableDeviceAsync(instanceId, cancellationToken);

                // The device-removal callback cannot finish while this handle owns the source lock.
                await Assert.ThrowsAsync<TimeoutException>(
                    () => disableTask.WaitAsync(DeviceRemovalBlockedTimeout, cancellationToken));
            } finally {
                // The holder must release the Monitor from the same thread that acquired it.
                releaseHandle.Set();
                await holderTask;
            }

            // Releasing the handle lets the callback complete and the source detach the device.
            await disableTask.WaitAsync(XenIfaceSourceFixture.PnputilTimeout, cancellationToken);
            Assert.Null(fixture.Source.Active);
        } finally {
            await fixture.EnableDeviceAsync(instanceId, cancellationToken);
        }
    }
}
