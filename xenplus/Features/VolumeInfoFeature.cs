using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using XenPlus.VolumeInfo;
using XenPlus.XenIface;

namespace XenPlus.Features;

sealed class VolumeInfoOptions {
    public bool Enabled { get; set; } = true;
    [Range(2, 999_999)]
    public int ReportIntervalMinutes { get; set; } = 59;
    public bool ReportAllDrives { get; set; } = false;
}

[OptionsValidator]
partial class ValidateVolumeInfoOptions : IValidateOptions<VolumeInfoOptions> {
}

sealed class VolumeInfoFeature(IHostLifetime _hostLifetime,
    IOptionsMonitor<VolumeInfoOptions> _options,
    XenIfaceSource _xi,
    ILogger<VolumeInfoFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    void Report(object? sender, XenIfaceResumedEventArgs args) {
        try {
            using var h = _xi.Lock();

            try {
                h.StoreRemove("data/volumes");
            } catch {
            }

            // HACK: For some reason, XAPI device names are always "xvd*" regardless of target number.
            // We need to detect XAPI to show a device name that matches what it does.
            var xapiMode = h.StoreTryRead("xenserver") != null;

            var vbds = VbdStore.GetVbds(h);
            var targetToName = new Dictionary<uint, string>();
            foreach (var (_, number) in vbds) {
                var (prefix, targetId) = VbdStore.VbdNumberToTargetId(number);
                targetToName[targetId] = VbdStore.FormatTargetName(xapiMode ? "xvd" : prefix, targetId);
            }

            var diskNumberToTarget = XenDiskStore.GetXenDisks()
                .ToDictionary(x => x.DiskNumber, x => x.TargetId);

            var volumes = VolumeStore.GetVolumeInfo(!_options.CurrentValue.ReportAllDrives).ToList();

            for (int i = 0; i < volumes.Count; i++) {
                var volume = volumes[i];

                try {
                    h.StoreWrite($"data/volumes/{i}/name", volume.ObjectPath);
                    h.StoreWrite($"data/volumes/{i}/size", volume.Size.ToString());
                    h.StoreWrite($"data/volumes/{i}/free", volume.Free.ToString());
                    h.StoreWrite($"data/volumes/{i}/filesystem", volume.Filesystem.ToString());
                    h.StoreWrite($"data/volumes/{i}/volume_name", volume.Label.ToString());

                    for (int j = 0; j < volume.MountPoints.Count; j++) {
                        var mountPoint = volume.MountPoints[j];

                        h.StoreWrite($"data/volumes/{i}/mount_points/{j}", mountPoint);

                        // is this a drive letter?
                        if (mountPoint.Length < 2 || mountPoint.Length > 3) {
                            continue;
                        }
                        if (mountPoint[1] != ':') {
                            continue;
                        }
                        if (mountPoint.Length == 3 && !Path.EndsInDirectorySeparator(mountPoint)) {
                            continue;
                        }
                        var driveLetter = char.ToUpperInvariant(mountPoint[0]);
                        if (driveLetter < 'A' || driveLetter > 'Z') {
                            continue;
                        }

                        h.StoreWrite($"data/volumes/{i}/driveletter", $"{driveLetter}:");
                    }

                    for (int j = 0; j < volume.Extents.Count; j++) {
                        var extent = volume.Extents[j];

                        h.StoreWrite($"data/volumes/{i}/extents/{j}/diskid", extent.DiskNumber.ToString());
                        h.StoreWrite($"data/volumes/{i}/extents/{j}/offset", extent.StartingOffset.ToString());
                        h.StoreWrite($"data/volumes/{i}/extents/{j}/length", extent.ExtentLength.ToString());
                        if (diskNumberToTarget.TryGetValue(extent.DiskNumber, out var targetId)) {
                            h.StoreWrite($"data/volumes/{i}/extents/{j}/target", targetId.ToString());
                            if (targetToName.TryGetValue(targetId, out var targetName)) {
                                h.StoreWrite($"data/volumes/{i}/extents/{j}", targetName);
                            }
                        }
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Cannot report volume info for '{}'", volume.ObjectPath);
                }
            }
        } catch (XenIfaceNotFoundException) {
        } catch (Exception ex) {
            _logger.LogError(ex, "{} report error", nameof(VolumeInfoFeature));
        }
    }

    void Cleanup() {
        try {
            using var h = _xi.Lock();

            h.StoreRemove("data/volumes");
        } catch (Exception ex) {
            _logger.LogDebug(ex, "{} cleanup error", nameof(VolumeInfoFeature));
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
                await Task.Delay(TimeSpan.FromMinutes(_options.CurrentValue.ReportIntervalMinutes), stoppingToken);
            }
        } finally {
            if (registered) {
                _xi.Resumed -= Report;
            }
            Cleanup();
        }
    }
}
