using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallProducts {
        static readonly List<string> KnownUpgradeCodes = new() {
            // x64 drivers version 6.6.557 from XS 6.6.90
            // must be uninstalled in the correct order
            // Citrix XenServer Tools Installer
            "{21EF141F-9126-42DA-93CD-B50442047420}",
            // Citrix XenServer Windows Guest Agent
            "{48E5492C-6843-452E-97A2-A5FE2D24B141}",
            // Citrix XenServer VSS Provider
            "{D8709720-65B7-4CD9-9F51-68DB592B604D}",
            // Citrix Xen Windows x64 PV Drivers
            "{53858014-F814-49A1-9D63-CA2578432E73}",

            // Citrix uses the same package code as the Citrix XenServer Windows Guest Agent
            // in their multi-package XS 6.6 drivers for their 7.1 series drivers
            // (aka. 48E5492C-6843-452E-97A2-A5FE2D24B141)
            // XCP-ng 8.2 x64 also uses the same upgrade code.

            // Citrix Hypervisor/XS8
            "{AF9B2559-3E91-4206-98C2-F560009FF7F1}",

            // generic x86 (does not work due to check in Invoke-XenClean)
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
                var msiexecPath = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
                foreach (var moObject in moObjects) {
                    Logger.Log($"Uninstalling product {moObject["ProductCode"]}");
                    using var msiexecProcess = Process.Start(
                        msiexecPath,
                        $"/x \"{moObject["ProductCode"]}\" /passive /norestart");
                    msiexecProcess.WaitForExit();
                    if (msiexecProcess.ExitCode != 0) {
                        Logger.Log($"Msiexec exited with code {msiexecProcess.ExitCode}");
                    }
                }
            }
        }
    }
}
