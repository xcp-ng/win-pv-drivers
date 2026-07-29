using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace XenDriverUtils {
    public class XenOffboard {
        enum ScriptMode {
            Backup,
            Restore,
        }

        enum ExecutionMode {
            Install,
            Invoke,
        }

        enum DeviceType {
            Paravirtualized,
            Emulated,
        }

        static string ExtractCopyXenvifScript(DirectoryInfo tempdir) {
            var scriptPath = Path.Combine(tempdir.FullName, "Copy-XenVifSettings.ps1");

            var resourceName = nameof(XenDriverUtils) + ".Copy-XenVifSettings.signed.ps1";
            using var scriptData = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName) ?? throw new NullReferenceException();
            using var scriptFile = File.Create(scriptPath);

            var buffer = new byte[4096];
            while (true) {
                var count = scriptData.Read(buffer, 0, buffer.Length);
                if (count == 0) {
                    break;
                }
                scriptFile.Write(buffer, 0, count);
            }

            return scriptPath;
        }

        static void RunCopyXenvifScript(ScriptMode mode, ExecutionMode execMode, DeviceType deviceType, bool dryRun) {
            var powershellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell\\v1.0\\powershell.exe");
            var tempdir = PathUtils.CreateSecureTempDirectory();

            try {
                var scriptPath = ExtractCopyXenvifScript(tempdir);
                Logger.LogFormat(
                    LogLevel.Info,
                    "Running {0} mode={1} execMode={2} deviceType={3} {4}",
                    scriptPath,
                    mode,
                    execMode,
                    deviceType,
                    dryRun ? "(dry-run)" : "");

                using var process = ProcessRedirector.LogCommand(
                    powershellPath,
                    $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -{mode} -{execMode} -{deviceType} {(dryRun ? "-WhatIf" : "")}",
                    TimeSpan.FromMinutes(5),
                    LogLevel.Info);

                if (process.ExitCode != 0) {
                    Logger.LogFormat(LogLevel.Alert, "Copy-XenVifSettings.ps1 error {0}: {1}", process.ExitCode);
                    throw new Exception($"Copy-XenVifSettings.ps1 error {process.ExitCode}");
                }
            } finally {
                try {
                    tempdir.Delete(true);
                } catch {
                }
            }
        }

        public static void BackupXenvif(bool dryRun) {
            Logger.Log("Backing up Xenvif settings");
            RunCopyXenvifScript(ScriptMode.Backup, ExecutionMode.Invoke, DeviceType.Paravirtualized, dryRun: dryRun);
        }

        public static void PrepareRestoreXenvif(bool dryRun) {
            Logger.Log("Scheduling Xenvif restore");
            RunCopyXenvifScript(ScriptMode.Restore, ExecutionMode.Install, DeviceType.Emulated, dryRun: dryRun);
        }

        public static bool IsReadyForCopyXenvif() {
            var schtasksPath = Path.Combine(Environment.SystemDirectory, "schtasks.exe");

            using var process = ProcessRedirector.LogCommand(
                schtasksPath,
                "/query /tn Copy-XenVifSettings /hresult",
                TimeSpan.FromMinutes(1),
                LogLevel.Info);

            // 0x80070002/0x80070003 (ERROR_FILE_NOT_FOUND/ERROR_PATH_NOT_FOUND in HRESULT form)
            return process.ExitCode == -2147024894 || process.ExitCode == -2147024893;
        }

        static readonly TimeSpan CopyXenvifPollInterval = TimeSpan.FromSeconds(3);

        public static bool WaitUntilReadyForCopyXenvif(TimeSpan timeout) {
            if (timeout < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            if (IsReadyForCopyXenvif()) {
                return true;
            }

            Logger.LogFormat(
                LogLevel.Info,
                "Waiting {0} seconds for Copy-XenVifSettings to disappear",
                timeout.TotalSeconds);

            var deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline) {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) {
                    break;
                }
                Thread.Sleep(remaining < CopyXenvifPollInterval ? remaining : CopyXenvifPollInterval);

                if (IsReadyForCopyXenvif()) {
                    Logger.Log(LogLevel.Info, "Copy-XenVifSettings task disappeared");
                    return true;
                }
            }

            Logger.Log(LogLevel.Alert, "Copy-XenVifSettings task did not disappear");
            return false;
        }

        public static bool WaitUntilReadyForCopyXenvif() {
            return WaitUntilReadyForCopyXenvif(TimeSpan.FromSeconds(120));
        }
    }
}
