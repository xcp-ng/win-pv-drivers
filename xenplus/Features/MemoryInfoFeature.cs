using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    IOptionsMonitor<MemoryInfoOptions> _options,
    XenIfaceSource _xi,
    ILogger<MemoryInfoFeature> _logger) : BackgroundService {
    void Report() {
        _logger.LogTrace("{}.{}", nameof(MemoryInfoFeature), nameof(Report));
        try {
            MEMORYSTATUSEX status;
            unsafe {
                status = new MEMORYSTATUSEX {
                    dwLength = (uint)sizeof(MEMORYSTATUSEX)
                };
            }
            if (!PInvoke.GlobalMemoryStatusEx(ref status)) {
                throw new Win32Exception("cannot query memory status");
            }

            using var h = _xi.Lock();

            h.StoreWrite("data/meminfo_total", (status.ullTotalPhys >> 10).ToString());
            h.StoreWrite("data/meminfo_free", (status.ullAvailPhys >> 10).ToString());

            h.StoreWrite("data/updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(MemoryInfoFeature));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!_options.CurrentValue.Enabled) {
            return;
        }
        _logger.LogDebug("Starting {}", nameof(MemoryInfoFeature));
        while (true) {
            await Task.Delay(TimeSpan.FromSeconds(_options.CurrentValue.ReportIntervalSeconds), stoppingToken);
            if (stoppingToken.IsCancellationRequested) {
                break;
            }
            Report();
        }
    }
}
