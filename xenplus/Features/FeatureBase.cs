using Microsoft.Extensions.Hosting.WindowsServices;

namespace XenPlus.Features;

abstract class FeatureBase(IHostLifetime _hostLifetime, ILogger _logger) : BackgroundService {
    protected abstract Task ExecuteFeatureAsync(CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await ExecuteFeatureAsync(stoppingToken);
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            try {
                _logger.LogError(ex, "{} exited with exception", GetType().Name);
            } catch {
            }
            if (_hostLifetime is WindowsServiceLifetime windowsServiceLifetime &&
                windowsServiceLifetime.ExitCode == 0) {
                windowsServiceLifetime.ExitCode = ex.HResult;
            }
            throw;
        }
    }
}
