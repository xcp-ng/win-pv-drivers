using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

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

        static void RunCopyXenvifScript(ScriptMode mode, ExecutionMode execMode, DeviceType deviceType) {
            var dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var scriptPath = Path.Combine(dllPath, "Copy-XenVifSettings.ps1");
            var powershellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell\\v1.0\\powershell.exe");

            var startInfo = new ProcessStartInfo() {
                FileName = powershellPath,
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -{mode} -{execMode} -{deviceType}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process.WaitForExit();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0) {
                Logger.Log($"Copy-XenVifSettings.ps1 error {process.ExitCode}: {error}");
                throw new Exception($"Copy-XenVifSettings.ps1 error {process.ExitCode}");
            }

            Logger.Log($"Copy-XenVifSettings.ps1 output: {output}");
        }

        public static void BackupXenvif() {
            Logger.Log($"Backing up Xenvif settings");
            RunCopyXenvifScript(ScriptMode.Backup, ExecutionMode.Invoke, DeviceType.Paravirtualized);
        }

        public static void PrepareRestoreXenvif() {
            Logger.Log($"Scheduling Xenvif restore");
            RunCopyXenvifScript(ScriptMode.Restore, ExecutionMode.Install, DeviceType.Emulated);
        }
    }
}
