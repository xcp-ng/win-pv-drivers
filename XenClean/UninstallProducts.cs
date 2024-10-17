using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32.Foundation;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallProducts {
        static readonly List<string> KnownUpgradeCodes = new() {
            // XCP-ng 8.2 x86
            "{EE3B949D-C431-462B-B6DC-5BEDA078D772}",
            // XCP-ng 8.2 x64
            "{48E5492C-6843-452E-97A2-A5FE2D24B141}",
            // Citrix 9.3
            "{AF9B2559-3E91-4206-98C2-F560009FF7F1}",
            // generic x86
            "{10828840-D8A9-4953-B44A-1F1D3CD7ECB0}",
            // generic x64
            "{D60FED1E-316C-41B0-B7A5-E44951A82618}",
            // ours
            VersionInfo.MsiUpgradeCodeX86,
            VersionInfo.MsiUpgradeCodeX64,
        };

        public static void Execute() {
            foreach (var upgradeCode in KnownUpgradeCodes) {
                Logger.Log($"Trying to uninstall products with upgrade code {upgradeCode}");
                var moSearcher = new ManagementObjectSearcher(
                    $"SELECT ProductCode FROM Win32_Property WHERE Property='UpgradeCode' AND Value='{upgradeCode}'");
                var moObjects = moSearcher.Get();
                foreach (var moObject in moObjects) {
                    Logger.Log($"Uninstalling product {moObject["ProductCode"]}");
                    using var msiexecProcess = Process.Start("msiexec.exe", $"/x \"{moObject["ProductCode"]}\" /passive /norestart");
                    msiexecProcess.WaitForExit();
                    if (msiexecProcess.ExitCode != 0) {
                        Logger.Log($"Msiexec exited with code {msiexecProcess.ExitCode}");
                    }
                }
            }
        }
    }
}
