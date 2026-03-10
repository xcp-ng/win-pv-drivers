using System;
using System.Diagnostics;
using System.IO;
using XenClean;
using XenDriverUtils;

class Program {
    static bool IsOnboardCleanupNeeded(string onboardFamily, out ExitCode exitCode) {
        if (XenCleanup.IsSafeMode()) {
            Logger.Log(LogLevel.Alert, "Onboarding denied in Safe Mode");
            exitCode = ExitCode.OnboardDenied;
            return false;
        }

        var thirdparty = XenCleanup.Find3PStorageDrivers();
        if (thirdparty.Count > 0) {
            Logger.Log(LogLevel.Alert, "Onboarding denied due to 3rd-party storage drivers");
            exitCode = ExitCode.OnboardDenied;
            return false;
        }

        UninstallProducts.FindRelatedProducts(onboardFamily, out var foundCompatible, out var foundIncompatible);
        var foundDrivers = UninstallDrivers.FindDrivers();
        var foundXenbus = XenOnboard.HasIncompatibleXenbus();

        Logger.LogFormat(
            LogLevel.Info,
            "foundCompatible={0} foundIncompatible={1} foundDrivers={2} foundXenbus={3}",
            foundCompatible,
            foundIncompatible,
            foundDrivers,
            foundXenbus);

        if (foundCompatible && !foundIncompatible) {
            Logger.Log(LogLevel.Interactive, "Onboarding already completed");
            exitCode = ExitCode.AlreadyOnboarded;
            return false;
        } else if (!foundCompatible && !foundIncompatible && !foundDrivers) {
            if (foundXenbus) {
                Logger.Log(LogLevel.Alert, "Onboarding denied due to presence of incompatible Xenbus device");
                exitCode = ExitCode.OnboardDenied;
                return false;
            } else {
                Logger.Log(LogLevel.Interactive, "No drivers present, ready for onboard");
                exitCode = ExitCode.ReadyForOnboard;
                return false;
            }
        }

        exitCode = ExitCode.Error;
        return true;
    }

    static ExitCode DoCleanupTasks(bool dryRun) {
        if (XenCleanup.IsSafeMode()) {
            Logger.Log("Skipping product uninstallation and offboarding in Safe Mode");
        } else {
            if (!XenOffboard.IsReadyForCopyXenvif()) {
                Logger.Log(LogLevel.Alert, "Xenvif offboarding task found, needs reboot");
                return ExitCode.RebootPending;
            }

            UninstallProducts.Execute(dryRun);

            if (XenOffboard.IsReadyForCopyXenvif()) {
                // the uninstallers did not implement Xenvif offboard, it's up to us to do it
                XenOffboard.BackupXenvif(dryRun);
                XenOffboard.PrepareRestoreXenvif(dryRun);
            }
        }

        UninstallDevices.Execute(dryRun);
        UninstallDrivers.Execute(dryRun);
        UninstallServices.Execute(!XenCleanup.IsSafeMode(), dryRun);
        UninstallRegistry.Execute(dryRun);

        return ExitCode.CleaningSucceeded;
    }

    static int Main(string[] args) {
        var dryRun = false;
        var noReboot = false;
        var confirm = true;
        string onboardFamily = null;

        var exitCode = ExitCode.Error;

        for (int i = 0; i < args.Length; i++) {
            var arg = args[i];

            if ("-dryRun".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
                dryRun = true;
            } else if ("-noReboot".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
                noReboot = true;
            } else if ("-noConfirm".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
                confirm = false;
            } else if ("-onboard".Equals(arg, StringComparison.OrdinalIgnoreCase)) {
                onboardFamily = args[++i];
            } else {
                Console.WriteLine("Usage: XenClean [-dryRun] [-noReboot] [-noConfirm]");
                return (int)exitCode;
            }
        }

        var runName = $"xenclean_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fffffff}";
        string logPath = null;
        try {
            using var logger = new TempFileLogger();
            logPath = logger.LogPath;

            Logger.SetLogger(logger);
            Logger.LogFormat(LogLevel.Interactive, "Log path is {0}", logger.LogPath);

            var onboarding = !string.IsNullOrEmpty(onboardFamily);

            if (dryRun) {
                Logger.Log("Dry-run is enabled, actions are not taken for real");
            }
            if (onboarding) {
                Logger.LogFormat(LogLevel.Interactive, "Running in onboarding mode for family {0}", onboardFamily);
            }
            Logger.LogFormat(
                LogLevel.Info,
                "dryRun={0} noReboot={1} confirm={2} onboardFamily={3}",
                dryRun,
                noReboot,
                confirm,
                onboardFamily ?? "");

            if (confirm && Environment.UserInteractive && !Console.IsInputRedirected) {
                Console.Write("Remove drivers? {0}[y/n] ", noReboot ? "" : "(Auto-reboot after completion) ");
                if (!"y".Equals(Console.ReadLine(), StringComparison.OrdinalIgnoreCase)) {
                    Logger.Log("Operation canceled by user");
                    return (int)ExitCode.UserCanceled;
                }
            } else if (confirm) {
                Logger.Log("Running in non-interactive mode, skipping user confirmation");
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

            if (onboarding && !IsOnboardCleanupNeeded(onboardFamily, out exitCode)) {
                Logger.LogFormat(LogLevel.Interactive, "Cleanup skipped due to onboard status {0}", exitCode);
            } else {
                using var _ = new LogSection($"Executing cleanup {(dryRun ? "(dry-run)" : "")}");
                try {
                    exitCode = DoCleanupTasks(dryRun: dryRun);
                } catch (Exception ex) {
                    Logger.LogFormat(LogLevel.Alert, "Cleanup FAILED: {0} {1}", ex.HResult, ex.Message);
                }
                Logger.LogFormat(LogLevel.Interactive, "Cleanup task status is {0}", exitCode);
            }

            using (var _ = new LogSection("Postclean system state")) {
                DiagnosticUtils.LogSystemState();
            }

            if (exitCode == ExitCode.CleaningSucceeded) {
                if (noReboot) {
                    Logger.Log("Finished, you must restart!");
                } else {
                    Logger.Log("Automatically restarting");
                }
            }
        } finally {
            Logger.SetLogger(new ConsoleLogger());

            if (exitCode < ExitCode.ReadyForOnboard) {
                // Don't delete the temp file when we did something meaningful.
                var copiedLogPath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", runName + ".log");
                Logger.LogFormat(LogLevel.Interactive, "Program done, copying log to {0}", copiedLogPath);
                try {
                    File.Copy(logPath, copiedLogPath);
                } catch (Exception ex) {
                    Logger.LogFormat(LogLevel.Alert, "Cannot copy log ({0} {1}); find the full log at {2}", ex.HResult, ex.Message, logPath);
                }
            } else {
                Logger.Log("Deleting temporary log");
                try {
                    File.Delete(logPath);
                } catch {
                }
            }
        }

        if (exitCode == ExitCode.CleaningSucceeded && !noReboot && !dryRun) {
            var psi = new ProcessStartInfo() {
                FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                Arguments = "-r -f -t 15",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(psi).WaitForExit();
        }

        return (int)exitCode;
    }
}
