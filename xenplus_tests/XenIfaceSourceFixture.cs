using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using XenPlus.XenIface;

namespace XenPlus;

public sealed class XenIfaceSourceFixture : IDisposable {
    internal sealed class TempStorePath(XenIfaceSource source) : IDisposable {
        const int ErrorFileNotFound = 2;
        bool _disposed;

        internal string Path { get; } = $"data/xenplus-tests/{Guid.NewGuid():N}";

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, true)) {
                return;
            }

            try {
                using var h = source.Lock();
                h.StoreRemove(Path);
            } catch (XenIfaceNotFoundException) {
                // The device disappeared before cleanup; XenStore state is outside this process.
            } catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorFileNotFound) {
                // The test failed before creating the registered path.
            }
        }
    }

    static readonly TimeSpan DeviceTimeout = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan DeviceStateChangeTimeout = TimeSpan.FromSeconds(30);
    bool _active = true;

    internal XenIfaceSource Source { get; } = new(NullLogger<XenIfaceSource>.Instance);

    internal bool TryGetActiveDevicePath(out string? devicePath) {
        lock (Source.SyncRoot) {
            devicePath = Source.Active?.DevicePath;
            return devicePath != null;
        }
    }

    internal async Task WaitForDeviceStateAsync(bool active, CancellationToken cancellationToken) {
        var deadline = Stopwatch.GetTimestamp() + (long)(DeviceTimeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline) {
            if (TryGetActiveDevicePath(out _) == active) {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        throw new TimeoutException(
            $"XenIface device did not become {(active ? "active" : "inactive")} within {DeviceTimeout}");
    }

    internal async Task<string> RequireActiveDeviceAsync(CancellationToken cancellationToken) {
        Assert.SkipUnless(
            XenIfaceIoctlDeviceDiscovery.IsAdministrator(),
            "XenIface API tests require an elevated test process");

        if (!_active) {
            Assert.Skip("No active XenIface device was detected");
        }

        try {
            await WaitForDeviceStateAsync(active: true, cancellationToken);
        } catch (TimeoutException) {
            _active = false;
            Assert.Skip("No active XenIface device was detected");
        }

        Assert.True(TryGetActiveDevicePath(out var devicePath));
        return devicePath!;
    }

    internal static string DeviceInterfacePathToInstanceId(string devicePath) {
        var prefixLength =
            devicePath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            devicePath.StartsWith(@"\\.\", StringComparison.Ordinal) ?
            4 :
            throw new ArgumentException("Unexpected device interface path prefix", nameof(devicePath));
        var classGuid = devicePath.LastIndexOf("#{", StringComparison.Ordinal);
        if (classGuid <= prefixLength) {
            throw new ArgumentException("Device interface path has no class GUID suffix", nameof(devicePath));
        }

        return devicePath[prefixLength..classGuid].Replace('#', '\\');
    }

    internal async Task<string> RequireActiveDeviceInstanceIdAsync(CancellationToken cancellationToken) {
        var devicePath = await RequireActiveDeviceAsync(cancellationToken);
        return DeviceInterfacePathToInstanceId(devicePath);
    }

    internal TempStorePath GetTempStorePath() {
        return new TempStorePath(Source);
    }

    /// <summary>
    /// Returns the CfgMgr32 result without requiring success. Callers expecting a veto check for CR_REMOVE_VETOED.
    /// </summary>
    internal static Task<CONFIGRET> TryDisableDeviceAsync(string instanceId, CancellationToken cancellationToken) {
        // The disable blocks until PnP finishes query-remove and removal, which includes delivering notifications to
        // this process. Keep it off the caller's thread. The native call cannot be cancelled: a timeout or
        // cancellation only abandons the wait.
        return Task.Run(() => XenIfaceDeviceState.Disable(instanceId), cancellationToken)
            .WaitAsync(DeviceStateChangeTimeout, cancellationToken);
    }

    internal async Task DisableDeviceAsync(string instanceId, CancellationToken cancellationToken) {
        Cfgmgr32.CheckConfigret(await TryDisableDeviceAsync(instanceId, cancellationToken));
        // Device removal notifications should detach the active XenIfaceDevice.
        await WaitForDeviceStateAsync(active: false, cancellationToken);
    }

    internal async Task EnableDeviceAsync(string instanceId, CancellationToken cancellationToken) {
        XenIfaceDeviceState.Enable(instanceId);
        // Live-rebind tests verify their own Resumed/watch postconditions separately.
        await WaitForDeviceStateAsync(active: true, cancellationToken);
    }

    /// <summary>
    /// Recovery must outlive test cancellation so a cancelled test does not leave the device disabled.
    /// </summary>
    internal Task EnableDeviceAsync(string instanceId) {
        return EnableDeviceAsync(instanceId, CancellationToken.None);
    }

    public void Dispose() {
        Source.Dispose();
    }
}
