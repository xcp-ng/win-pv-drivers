using System;
using System.IO;
using XenDriverUtils;

namespace XenClean {
    class RestorePoint {
        public static void CreateRestorePoint(string runName, bool dryRun) {
            var powershellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell\\v1.0\\powershell.exe");

            using var process = ProcessRedirector.LogCommand(
                powershellPath,
                $"-ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description {runName} -RestorePointType APPLICATION_UNINSTALL {(dryRun ? "-WhatIf" : "")}\"",
                TimeSpan.FromMinutes(5));

            if (process.ExitCode != 0) {
                throw new Exception($"Checkpoint-Computer error {process.ExitCode}");
            }
        }
    }
}
