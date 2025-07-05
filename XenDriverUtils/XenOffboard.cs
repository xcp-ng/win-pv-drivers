using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace XenDriverUtils {
    public class XenOffboard {
        static void RunCopyXenvifScript(string mode, bool install = false) {
            var dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var scriptPath = Path.Combine(dllPath, "Copy-XenVifSettings.ps1");
            var powershellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell\\v1.0\\powershell.exe");

            var startInfo = new ProcessStartInfo() {
                FileName = powershellPath,
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {(install ? "-Install" : "-Invoke")} -Mode {mode}",
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
            RunCopyXenvifScript("Backup");
        }

        public static void PrepareRestoreXenvif() {
            Logger.Log($"Scheduling Xenvif restore");
            RunCopyXenvifScript("Restore", true);
        }
    }
}
