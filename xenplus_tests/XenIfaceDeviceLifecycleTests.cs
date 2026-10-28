using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using XenPlus.XenIface;

using static XenPlus.XenIfaceSourceTestSupport;

namespace XenPlus;

[CollectionDefinition("XenIfaceDeviceLifecycle", DisableParallelization = true)]
public sealed class XenIfaceDeviceLifecycleCollection {
}

// These tests disable the device until it is re-enabled or the next boot. Select them explicitly, in isolation.
[Collection("XenIfaceDeviceLifecycle")]
[Trait("Category", "XenIface")]
[Trait("Category", "XenIfaceDeviceLifecycle")]
public class XenIfaceDeviceLifecycleTests(XenIfaceSourceFixture fixture) : IClassFixture<XenIfaceSourceFixture> {
    [Fact(Explicit = true)]
    public async Task VetoedDisableRebindsSourceAndRearmsWatch() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        using var source = new XenIfaceSource(NullLogger<XenIfaceSource>.Instance);
        var original = await WaitForActiveDeviceAsync(source, cancellationToken);
        var instanceId = XenIfaceSourceFixture.DeviceInterfacePathToInstanceId(original.DevicePath);
        using var watch = source.WatchAdd(storePath.Path);
        using var blocker = new XenIfaceQueryRemoveBlocker(original.Handle);
        using var observer = new ResumedObserver(source, original);
        using var watchObserver = new WatchObserver(watch);
        try {
            await WaitForWatchReadyAsync(watch, cancellationToken);

            // No synthetic callbacks: keeping the duplicated native handle open makes Windows cancel removal.
            var disableResult = await XenIfaceSourceFixture.TryDisableDeviceAsync(instanceId, cancellationToken);
            Assert.Equal(CONFIGRET.CR_REMOVE_VETOED, disableResult);
            var failureNotification = await Task.WhenAny(
                blocker.QueryRemoveFailed,
                Task.Delay(EventTimeout, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(
                ReferenceEquals(failureNotification, blocker.QueryRemoveFailed),
                "Windows did not report QUERYREMOVEFAILED");
            Assert.True(blocker.QueryRemove.IsCompletedSuccessfully, "Windows did not report QUERYREMOVE");
            Assert.False(blocker.RemovalObserved, "Windows proceeded to removal despite the outstanding handle");

            var replacement = await observer.Resumed.WaitAsync(EventTimeout, cancellationToken);
            Assert.NotSame(original, replacement);
            Assert.Equal(original.DevicePath, replacement.DevicePath);
            Assert.True(original.Handle.IsClosed);

            // Drop the last handle to the old file object before checking watches. Otherwise its surviving driver
            // watches could produce notifications and mask a failure to rearm on the replacement device.
            blocker.Dispose();
            await WaitForWatchReadyAsync(watch, cancellationToken);
            watchObserver.Arm();
            using (var h = source.Lock()) {
                h.StoreWriteStrict(storePath.Path, "changed after disable veto");
                Assert.Equal("changed after disable veto", h.StoreReadStrict(storePath.Path));
            }
            await watchObserver.Triggered.WaitAsync(EventTimeout, cancellationToken);
        } finally {
            blocker.Dispose();
            watch.Dispose();
            source.Dispose();
            original.Dispose();
            // Re-enable the device after the assertions, even if cancellation was requested.
            await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Explicit = true)]
    public async Task DisableAndReenableDeviceRebindsSource() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var instanceId = await fixture.RequireActiveDeviceInstanceIdAsync(cancellationToken);
        using var observer = new ResumedObserver(fixture.Source, armed: false);

        try {
            await fixture.DisableDeviceAsync(instanceId, cancellationToken);
            observer.Arm();
        } finally {
            // Recovery must not use the test cancellation token: always try to leave the device enabled.
            await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
        }

        // Reopening the interface registers the suspend event and raises Resumed through SuspendCallback.
        await observer.Resumed.WaitAsync(XenIfaceSourceFixture.DeviceStateChangeTimeout, cancellationToken);
        Assert.NotNull(fixture.Source.Active);
    }

    [Fact(Explicit = true)]
    public async Task WatchIsRearmedAfterDisableAndReenable() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var instanceId = await fixture.RequireActiveDeviceInstanceIdAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        using var observer = new ResumedObserver(fixture.Source, armed: false);
        using var watch = fixture.Source.WatchAdd(storePath.Path);
        using var watchObserver = new WatchObserver(watch);
        await WaitForWatchReadyAsync(watch, cancellationToken);

        try {
            await fixture.DisableDeviceAsync(instanceId, cancellationToken);
            observer.Arm();
        } finally {
            await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
        }

        // Resumed is raised only after SuspendCallback has rearmed every registered watch.
        await observer.Resumed.WaitAsync(XenIfaceSourceFixture.DeviceStateChangeTimeout, cancellationToken);
        await WaitForWatchReadyAsync(watch, cancellationToken);

        // Ignore registration/rearm signals and require a subsequent store change to trigger the watch.
        watchObserver.Arm();
        using (var h = fixture.Source.Lock()) {
            h.StoreWriteStrict(storePath.Path, "changed after rebind");
        }
        await watchObserver.Triggered.WaitAsync(EventTimeout, cancellationToken);
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

                // DisableDeviceAsync queues the blocking disable before returning its incomplete task.
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
            await disableTask.WaitAsync(XenIfaceSourceFixture.DeviceStateChangeTimeout, cancellationToken);
            Assert.Null(fixture.Source.Active);
        } finally {
            await fixture.EnableDeviceAsync(instanceId, TestContext.Current.CancellationToken);
        }
    }
}
