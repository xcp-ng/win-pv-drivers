using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class MemoryInfoOptions {
    public bool Enabled { get; set; } = true;
    [Range(5, 999_999_999)]
    public int ReportIntervalSeconds { get; set; } = 59;
}

[OptionsValidator]
partial class ValidateMemoryInfoOptions : IValidateOptions<MemoryInfoOptions> {
}

sealed class MemoryInfoFeature(
    IHostLifetime _hostLifetime,
    IOptionsMonitor<MemoryInfoOptions> _options,
    XenIfaceSource _xi,
    ILogger<MemoryInfoFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    void Report(object? sender, XenIfaceResumedEventArgs args) {
        _logger.LogTrace("{}.{}", nameof(MemoryInfoFeature), nameof(Report));
        try {
            var status = new MEMORYSTATUSEX {
                dwLength = (uint)Unsafe.SizeOf<MEMORYSTATUSEX>()
            };
            if (!PInvoke.GlobalMemoryStatusEx(ref status)) {
                throw new Win32Exception("cannot query memory status");
            }

            using var h = _xi.Lock();

            h.StoreWrite("data/meminfo_total", (status.ullTotalPhys >> 10).ToString());
            h.StoreWrite("data/meminfo_free", (status.ullAvailPhys >> 10).ToString());
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(MemoryInfoFeature));
        }
    }

    void Cleanup() {
        try {
            using var h = _xi.Lock();

            h.StoreRemove("data/meminfo_total");
            h.StoreRemove("data/meminfo_free");
        } catch (Exception ex) {
            _logger.LogDebug(ex, "{} cleanup error", nameof(MemoryInfoFeature));
        }
    }

    protected override async Task ExecuteFeatureAsync(CancellationToken stoppingToken) {
        bool registered = false;
        try {
            if (!_options.CurrentValue.Enabled) {
                return;
            }
            _xi.Resumed += Report;
            registered = true;
            while (!stoppingToken.IsCancellationRequested) {
                Report(null, new());
                await Task.Delay(TimeSpan.FromSeconds(_options.CurrentValue.ReportIntervalSeconds), stoppingToken);
            }
        } finally {
            if (registered) {
                _xi.Resumed -= Report;
            }
            Cleanup();
        }
    }
}
