using System;
using System.Diagnostics;
using System.IO;
using XenClean;
using XenDriverUtils;

var dryRun = false;
var noReboot = false;
var confirm = true;

foreach (var arg in args) {
    if ("-dryRun".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
        dryRun = true;
    } else if ("-noReboot".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
        noReboot = true;
    } else if ("-noConfirm".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
        confirm = false;
    }
}

bool succeeded = false;
try {
    using var logger = new TempFileLogger();
    Logger.SetLogger(logger);
    Logger.LogFormat(LogLevel.Interactive, "Log path is {0}", logger.LogPath);

    var runName = $"xenclean_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fffffff}";

    if (dryRun) {
        Logger.Log("Dry-run is enabled, actions are not taken for real");
    }

    if (confirm && Environment.UserInteractive && !Console.IsInputRedirected) {
        Console.Write("Remove drivers? {0}[y/n] ", noReboot ? "" : "(Auto-reboot after completion) ");
        if (!"y".Equals(Console.ReadLine(), StringComparison.OrdinalIgnoreCase)) {
            Logger.Log("Operation canceled by user");
            return;
        }
    }

    using (var _ = new LogSection("Preclean system state")) {
        DiagnosticUtils.LogSystemState();
    }

    using (var _ = new LogSection("Creating restore point")) {
        try {
            RestorePoint.CreateRestorePoint(runName, dryRun: dryRun);
        } catch (Exception ex) {
            Logger.LogFormat(LogLevel.Info, "Could not create restore point: {0} {1}", ex.HResult, ex.Message);
        }
    }

    using (var _ = new LogSection($"Executing {(dryRun ? "(dry-run)" : "")}")) {
        try {
            if (XenCleanup.IsSafeMode()) {
                Logger.Log("Skipping Xenvif offboarding in Safe Mode");
            } else {
                XenOffboard.BackupXenvif(dryRun);
                XenOffboard.PrepareRestoreXenvif(dryRun);
            }
            if (XenCleanup.IsSafeMode()) {
                Logger.Log("Skipping product uninstallation in Safe Mode");
            } else {
                UninstallProducts.Execute(dryRun);
            }
            UninstallDevices.Execute(dryRun);
            UninstallDrivers.Execute(dryRun);
            UninstallServices.Execute(!XenCleanup.IsSafeMode(), dryRun);
            UninstallRegistry.Execute(dryRun);
            succeeded = true;
        } catch (Exception ex) {
            Logger.LogFormat(LogLevel.Alert, "Cleanup FAILED: {0} {1}", ex.HResult, ex.Message);
        }
    }

    using (var _ = new LogSection("Postclean system state")) {
        DiagnosticUtils.LogSystemState();
    }

    if (succeeded) {
        if (noReboot) {
            Logger.Log("Finished, you must restart!");
        } else {
            Logger.Log("Automatically restarting");
        }
    }

    var copiedLogPath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", runName + ".log");
    Logger.LogFormat(LogLevel.Interactive, "Program done, copying log to {0}", copiedLogPath);
    try {
        File.Copy(logger.LogPath, copiedLogPath);
    } catch (Exception ex) {
        Logger.LogFormat(LogLevel.Alert, "Cannot copy log ({0} {1}); find the full log at {2}", ex.HResult, ex.Message, logger.LogPath);
    }
} finally {
    Logger.SetLogger(null);
}

if (succeeded && !noReboot) {
    if (!dryRun) {
        var psi = new ProcessStartInfo() {
            FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
            Arguments = "-r -f -t 15",
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process.Start(psi).WaitForExit();
    }
}

