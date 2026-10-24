using Microsoft.Extensions.Logging.Abstractions;
using XenPlus.XenIface;

using static XenPlus.XenIfaceSourceTestSupport;

namespace XenPlus;

[CollectionDefinition("XenIfaceDeviceLifecycle", DisableParallelization = true)]
public sealed class XenIfaceDeviceLifecycleCollection {
}

// These tests change Windows device state and can leave a reboot required. Select them explicitly, in isolation.
[Collection("XenIfaceDeviceLifecycle")]
[Trait("Category", "XenIface")]
[Trait("Category", "XenIfaceDeviceLifecycle")]
public class XenIfaceDeviceLifecycleTests(XenIfaceSourceFixture fixture) : IClassFixture<XenIfaceSourceFixture> {
    [Fact(Explicit = true)]
    public async Task PnputilVetoedDisableRebindsSourceAndRearmsWatch() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        using var source = new XenIfaceSource(NullLogger<XenIfaceSource>.Instance);
        var original = await WaitForActiveDeviceAsync(source, cancellationToken);
        var instanceId = XenIfaceSourceFixture.DeviceInterfacePathToInstanceId(original.DevicePath);
        using var watch = source.WatchAdd(storePath.Path);
        using var blocker = new XenIfaceQueryRemoveBlocker(original.Handle);
        var rebound = new TaskCompletionSource<XenIfaceDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var watchTriggered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observeWatch = false;
        Exception? testFailure = null;

        void OnResumed(object? sender, XenIfaceResumedEventArgs args) {
            lock (source.SyncRoot) {
                if (source.Active is XenIfaceDevice active && !ReferenceEquals(active, original)) {
                    rebound.TrySetResult(active);
                }
            }
        }

        void OnWatchTriggered(object? sender, XenIfaceWatchEventArgs args) {
            if (Volatile.Read(ref observeWatch)) {
                watchTriggered.TrySetResult();
            }
        }

        source.Resumed += OnResumed;
        watch.WatchTriggered += OnWatchTriggered;
        try {
            await WaitForWatchReadyAsync(watch, cancellationToken);

            // No synthetic callbacks: keeping the duplicated native handle open makes Windows cancel removal.
            var (ExitCode, Output) = await fixture.TryDisableDeviceAsync(instanceId, cancellationToken);
            var failureNotification = await Task.WhenAny(
                blocker.QueryRemoveFailed,
                Task.Delay(EventTimeout, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(
                ReferenceEquals(failureNotification, blocker.QueryRemoveFailed),
                $"Windows did not report QUERYREMOVEFAILED. pnputil exit code {ExitCode}:\n{Output}");
            Assert.True(blocker.QueryRemove.IsCompletedSuccessfully, "Windows did not report QUERYREMOVE");
            Assert.False(blocker.RemovalObserved, "Windows proceeded to removal despite the outstanding handle");

            var replacement = await rebound.Task.WaitAsync(EventTimeout, cancellationToken);
            Assert.NotSame(original, replacement);
            Assert.Equal(original.DevicePath, replacement.DevicePath);
            Assert.True(original.Handle.IsClosed);

            // Drop the last handle to the old file object before checking watches. Otherwise its surviving driver
            // watches could produce notifications and mask a failure to rearm on the replacement device.
            blocker.Dispose();
            await WaitForWatchReadyAsync(watch, cancellationToken);
            Volatile.Write(ref observeWatch, true);
            using (var h = source.Lock()) {
                h.StoreWriteStrict(storePath.Path, "changed after pnputil veto");
                Assert.Equal("changed after pnputil veto", h.StoreReadStrict(storePath.Path));
            }
            await watchTriggered.Task.WaitAsync(EventTimeout, cancellationToken);
        } catch (Exception ex) {
            testFailure = ex;
            throw;
        } finally {
            source.Resumed -= OnResumed;
            watch.WatchTriggered -= OnWatchTriggered;
            blocker.Dispose();
            watch.Dispose();
            source.Dispose();
            original.Dispose();
            // Restore the enabled configuration through SetupAPI after the assertions, even if cancellation was
            // requested. Report any remaining reboot requirement and preserve both test and cleanup failures.
            await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Explicit = true)]
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
                await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
            }

            // Reopening the interface registers the suspend event and raises Resumed through SuspendCallback.
            await resumed.Task.WaitAsync(XenIfaceSourceFixture.PnputilTimeout, cancellationToken);
            Assert.NotNull(fixture.Source.Active);
        } finally {
            fixture.Source.Resumed -= onResumed;
        }
    }

    [Fact(Explicit = true)]
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
                await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
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

    [Fact(Explicit = true)]
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
            await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
        }
    }
}
