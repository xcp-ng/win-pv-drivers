using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Logging.Abstractions;
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
    internal static readonly TimeSpan PnputilTimeout = TimeSpan.FromSeconds(30);
    bool _active = true;

    internal XenIfaceSource Source { get; } = new(NullLogger<XenIfaceSource>.Instance);

    static bool IsAdministrator() {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

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
            IsAdministrator(),
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

    static async Task<(int ExitCode, string Output)> RunPnputilAsync(
        string command,
        string instanceId,
        CancellationToken cancellationToken,
        bool requireSuccess = true) {

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PnputilTimeout);

        var startInfo = new ProcessStartInfo {
            FileName = Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(instanceId);

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("pnputil did not start");
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);

        try {
            await process.WaitForExitAsync(timeout.Token);
        } catch {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        var output = await stdout;
        var error = await stderr;
        if (requireSuccess && process.ExitCode != 0) {
            throw new InvalidOperationException(
                $"pnputil {command} failed with exit code {process.ExitCode}: {output}{error}");
        }
        return (process.ExitCode, $"{output}{error}");
    }

    internal Task<(int ExitCode, string Output)> TryDisableDeviceAsync(
        string instanceId,
        CancellationToken cancellationToken) {
        // A veto is expected by the caller. Do not require success or wait for the source to become inactive.
        return RunPnputilAsync("/disable-device", instanceId, cancellationToken, requireSuccess: false);
    }

    internal async Task DisableDeviceAsync(string instanceId, CancellationToken cancellationToken) {
        await RunPnputilAsync("/disable-device", instanceId, cancellationToken);
        // Device removal notifications should detach the active XenIfaceDevice.
        await WaitForDeviceStateAsync(active: false, cancellationToken);
    }

    internal async Task EnableDeviceAsync(string instanceId, CancellationToken cancellationToken) {
        var rebootRequired = XenIfaceDeviceState.Enable(instanceId);
        TestContext.Current.TestOutputHelper?.WriteLine(
            rebootRequired ?
                $"Enabled {instanceId} through SetupAPI; Windows still reports a reboot is required." :
                $"Enabled {instanceId} through SetupAPI.");

        // A successful configuration change with DI_NEEDREBOOT/DI_NEEDRESTART is not an enable failure.
        // Live-rebind tests verify their own Resumed/watch postconditions separately.
        if (!rebootRequired) {
            await WaitForDeviceStateAsync(active: true, cancellationToken);
        }
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
