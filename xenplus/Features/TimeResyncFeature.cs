using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Windows.Win32.Foundation;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class TimeResyncOptions {
    public bool Enabled { get; set; } = true;
    [Range(100, 3_600_000)]
    public int CommandTimeoutMilliseconds { get; set; } = 5000;
}

[OptionsValidator]
partial class ValidateTimeResyncOptions : IValidateOptions<TimeResyncOptions> {
}

sealed class TimeResyncFeature(
    IHostLifetime _hostLifetime,
    IOptionsMonitor<TimeResyncOptions> _options,
    XenIfaceSource _xi,
    ILogger<TimeResyncFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    static readonly string W32tmPath = Path.Combine(Environment.SystemDirectory, "w32tm.exe");
    static readonly int HresultErrorServiceDoesNotExist = ServerUtils.HresultFromWin32(
        (int)WIN32_ERROR.ERROR_SERVICE_DOES_NOT_EXIST);
    static readonly int HresultErrorServiceNotActive = ServerUtils.HresultFromWin32(
        (int)WIN32_ERROR.ERROR_SERVICE_NOT_ACTIVE);

    readonly ReferenceCount _alive = new();
    CancellationTokenSource? _cts = null;

    static void ThrowIfW32tmFailed(int exitCode) {
        if (exitCode == 0 ||
            exitCode == HresultErrorServiceDoesNotExist ||
            exitCode == HresultErrorServiceNotActive) {
            return;
        }
        throw exitCode < 0 ? new COMException(null, exitCode) : new Win32Exception(exitCode);
    }

    async void OnResume(object? sender, XenIfaceResumedEventArgs args) {
        using var lifetime = _alive.TryEnterScope();
        if (lifetime == null) {
            return;
        }

        _logger.LogTrace("{}.{}", nameof(TimeResyncFeature), nameof(OnResume));
        try {
            var psi = new ProcessStartInfo() {
                FileName = W32tmPath,
                Arguments = "/resync /nowait /force",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi) ?? throw new NullReferenceException("w32tm process not started");
            process.StandardInput.Close();

            using var stdout = process.StandardOutput;
            using var stderr = process.StandardError;
            _ = stdout.BaseStream.CopyToAsync(Stream.Null, _cts!.Token);
            _ = stderr.BaseStream.CopyToAsync(Stream.Null, _cts!.Token);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts!.Token);
            timeout.CancelAfter(_options.CurrentValue.CommandTimeoutMilliseconds);

            try {
                await process.WaitForExitAsync(timeout.Token);
            } catch (OperationCanceledException) {
                process.Kill();
                process.WaitForExit();
            }
            ThrowIfW32tmFailed(process.ExitCode);
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(TimeResyncFeature));
        }
    }

    protected override async Task ExecuteFeatureAsync(CancellationToken stoppingToken) {
        bool registered = false;
        try {
            if (!_options.CurrentValue.Enabled) {
                return;
            }
            _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _xi.Resumed += OnResume;
            registered = true;
            await Task.Delay(Timeout.Infinite, stoppingToken);
        } finally {
            if (registered) {
                _xi.Resumed -= OnResume;
            }
            await _alive.RundownAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);
            _cts?.Dispose();
            _cts = null;
        }
    }
}
