using Microsoft.Extensions.Hosting.WindowsServices;

namespace XenPlus.Features;

abstract class FeatureBase(IHostLifetime _hostLifetime, ILogger _logger) : BackgroundService {
    protected abstract Task ExecuteFeatureAsync(CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            _logger.LogDebug("Starting feature {}", GetType().Name);
            await ExecuteFeatureAsync(stoppingToken);
            _logger.LogDebug("Feature {} exited", GetType().Name);
        } catch (OperationCanceledException) {
            try {
                _logger.LogDebug("Feature {} was stopped", GetType().Name);
            } catch {
            }
        } catch (Exception ex) {
            try {
                _logger.LogError(ex, "Feature {} exited with exception", GetType().Name);
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
