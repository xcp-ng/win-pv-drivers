using Microsoft.Extensions.Logging.Abstractions;
using XenPlus.XenIface;

using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_NOTIFY_ACTION;
using static XenPlus.XenIfaceSourceTestSupport;

namespace XenPlus;

[Trait("Category", "XenIface")]
public class XenIfaceSourceIntegrationTests(XenIfaceSourceFixture fixture) : IClassFixture<XenIfaceSourceFixture> {
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueryRemoveFailedRebindsSource(bool queryRemoveFirst) {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        // Each case owns its source so a failed recovery cannot contaminate the shared fixture. The detached original is
        // also cleaned up, after the source, in case a regression keeps the worker from retiring its notification.
        using var detached = new DisposeStack();
        using var source = new XenIfaceSource(NullLogger<XenIfaceSource>.Instance);
        var original = await WaitForActiveDeviceAsync(source, cancellationToken);
        detached.Push(original);
        using var observer = new ResumedObserver(source, original);

        // Inject notifications at the managed callback boundary. The device and all subsequent IOCTLs are real;
        // no Windows device disable/enable or arrival notification is used to rescue canceled removal.
        if (queryRemoveFirst) {
            source.OnDeviceCallback(CM_NOTIFY_ACTION_DEVICEQUERYREMOVE, original);
            lock (source.SyncRoot) {
                Assert.Null(source.Active);
                Assert.True(original.Handle.IsClosed);
            }

            // Query-remove alone must not reopen the device before Windows reports its outcome.
            await Assert.ThrowsAsync<TimeoutException>(
                () => observer.Resumed.WaitAsync(DeviceRemovalBlockedTimeout, cancellationToken));
            lock (source.SyncRoot) {
                Assert.Null(source.Active);
            }
        }

        // Windows can also send QUERYREMOVEFAILED without a preceding QUERYREMOVE.
        source.OnDeviceCallback(CM_NOTIFY_ACTION_DEVICEQUERYREMOVEFAILED, original);
        var replacement = await observer.Resumed.WaitAsync(EventTimeout, cancellationToken);
        Assert.NotSame(original, replacement);
        Assert.Equal(original.DevicePath, replacement.DevicePath);
        Assert.True(original.Handle.IsClosed);

        using var h = source.Lock();
        h.StoreWriteStrict(storePath.Path, "recovered after canceled removal");
        Assert.Equal("recovered after canceled removal", h.StoreReadStrict(storePath.Path));
    }

    [Fact]
    public async Task WatchIsRearmedAfterQueryRemoveFailed() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.RequireActiveDeviceAsync(cancellationToken);
        using var storePath = fixture.GetTempStorePath();
        using var detached = new DisposeStack();
        using var source = new XenIfaceSource(NullLogger<XenIfaceSource>.Instance);
        var original = await WaitForActiveDeviceAsync(source, cancellationToken);
        detached.Push(original);
        using var watch = source.WatchAdd(storePath.Path);
        using var observer = new ResumedObserver(source, original);
        using var watchObserver = new WatchObserver(watch);

        await WaitForWatchReadyAsync(watch, cancellationToken);
        source.OnDeviceCallback(CM_NOTIFY_ACTION_DEVICEQUERYREMOVE, original);
        source.OnDeviceCallback(CM_NOTIFY_ACTION_DEVICEQUERYREMOVEFAILED, original);

        // No REMOVEPENDING, REMOVECOMPLETE or interface arrival follows canceled removal.
        await observer.Resumed.WaitAsync(EventTimeout, cancellationToken);
        await WaitForWatchReadyAsync(watch, cancellationToken);

        watchObserver.Arm();
        using (var h = source.Lock()) {
            h.StoreWriteStrict(storePath.Path, "changed after canceled removal");
        }
        await watchObserver.Triggered.WaitAsync(EventTimeout, cancellationToken);
    }
}
