using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Win32.Foundation;
using XenDriverUtils;

namespace XenClean {
    class KnownProduct {
        public IReadOnlyList<string> OnboardFamilies { get; }
        public bool HasXenvifOffboard { get; }
        // TODO: describe the range of product versions that support Xenvif offboarding instead of just a bool

        public KnownProduct(IEnumerable<string> OnboardFamilies, bool HasXenvifOffboard) {
            this.OnboardFamilies = new List<string>(OnboardFamilies);
            this.HasXenvifOffboard = HasXenvifOffboard;
        }
    }

    static class UninstallProducts {
        // maps from upgrade code to known product
        static readonly IReadOnlyDictionary<string, KnownProduct> KnownProducts = new Dictionary<string, KnownProduct>() {
            // x64 drivers version 6.6.557 from XS 6.6.90
            // must be uninstalled in the correct order
            // Citrix XenServer Tools Installer
            { "{21EF141F-9126-42DA-93CD-B50442047420}", new KnownProduct(new List<string>() { "XenServer" }, false) },
            // Citrix XenServer Windows Guest Agent
            { "{48E5492C-6843-452E-97A2-A5FE2D24B141}", new KnownProduct(new List<string>() { "XenServer" }, false) },
            // Citrix XenServer VSS Provider
            { "{D8709720-65B7-4CD9-9F51-68DB592B604D}", new KnownProduct(new List<string>() { "XenServer" }, false) },
            // Citrix Xen Windows x64 PV Drivers
            { "{53858014-F814-49A1-9D63-CA2578432E73}", new KnownProduct(new List<string>() { "XenServer" }, false) },

            // Citrix uses the same package code as the Citrix XenServer Windows Guest Agent
            // in their multi-package XS 6.6 drivers for their 7.1 series drivers
            // (aka. 48E5492C-6843-452E-97A2-A5FE2D24B141)
            // XCP-ng 8.2 x64 also uses the same upgrade code.

            // Citrix Hypervisor/XS8
            { "{AF9B2559-3E91-4206-98C2-F560009FF7F1}", new KnownProduct(new List<string>() { "XenServer", "XCP-ng" }, false) },

            // generic x86 (does not work due to check in Invoke-XenClean)
            { "{10828840-D8A9-4953-B44A-1F1D3CD7ECB0}", new KnownProduct(new List<string>() { "Unknown" }, true) },
            // generic x64
            { "{D60FED1E-316C-41B0-B7A5-E44951A82618}", new KnownProduct(new List<string>() { "Unknown" }, true) },

            // ours
            { VersionInfo.MsiUpgradeCodeX86, new KnownProduct(new List<string>() { VersionInfo.VendorName }, true) },
            { VersionInfo.MsiUpgradeCodeX64, new KnownProduct(new List<string>() { VersionInfo.VendorName }, true) },
        };

        public static List<string> FindProducts(out bool foundHasXenvifOffboard) {
            foundHasXenvifOffboard = false;
            var result = new List<string>();

            Logger.Log(LogLevel.Interactive, "Finding existing products");
            foreach (var entry in KnownProducts) {
                Logger.LogFormat(LogLevel.Info, "Finding products with upgrade code {0}", entry.Key);
                var products = ProductUtils.EnumerateProducts(entry.Key);

                foreach (var productCode in products) {
                    Logger.LogFormat(LogLevel.Info, "Found product {0}", productCode);
                    result.Add(productCode);
                }

                if (products.Count > 0 && entry.Value.HasXenvifOffboard) {
                    foundHasXenvifOffboard = true;
                }
            }

            return result;
        }

        public static void Execute(IEnumerable<string> productCodes, bool dryRun) {
            var msiexecPath = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
            var msiLogDir = PathUtils.CreateSecureTempDirectory();

            foreach (var upgradeCode in KnownProducts.Keys) {
                Logger.LogFormat(LogLevel.Info, "Trying to uninstall products with upgrade code {0}", upgradeCode);

                foreach (var productCode in productCodes) {
                    Logger.LogFormat(LogLevel.Interactive, "Uninstalling product {0}", productCode);
                    var logPath = Path.Combine(msiLogDir.FullName, Regex.Replace(productCode, "[^a-z0-9-_]", "", RegexOptions.IgnoreCase) + ".log");
                    Logger.LogFormat(LogLevel.Info, "Msiexec log path is {0}", logPath);
                    if (!dryRun) {
                        using var msiexecProcess = Process.Start(msiexecPath, $"/x \"{productCode}\" /passive /norestart /log \"{logPath}\"");
                        msiexecProcess.WaitForExit();
                        Logger.LogFormat(LogLevel.Interactive, "Msiexec exited with code {0}", msiexecProcess.ExitCode);
                        try {
                            using var msiLog = File.OpenText(logPath);
                            while (true) {
                                var line = msiLog.ReadLine();
                                if (line == null) {
                                    break;
                                }
                                Logger.Log(LogLevel.Trace, line);
                            }
                        } catch (Exception ex) {
                            Logger.LogFormat(LogLevel.Alert, "Cannot read MSI uninstallation log: {0} {1}", ex.HResult, ex.Message);
                        }
                        if (msiexecProcess.ExitCode != 0 && msiexecProcess.ExitCode != (int)WIN32_ERROR.ERROR_SUCCESS_REBOOT_REQUIRED) {
                            throw new Win32Exception(msiexecProcess.ExitCode, $"Msiexec failed with code {msiexecProcess.ExitCode}");
                        }
                    }
                }
            }
        }

        public static void FindRelatedProducts(string onboardFamily, out bool foundCompatible, out bool foundIncompatible) {
            foundCompatible = false;
            foundIncompatible = false;

            Logger.LogFormat(LogLevel.Interactive, "Finding products for onboarding of family {0}", onboardFamily);

            foreach (var entry in KnownProducts) {
                Logger.LogFormat(LogLevel.Info, "Finding products with upgrade code {0}", entry.Key);
                var products = ProductUtils.EnumerateProducts(entry.Key);

                foreach (var productCode in products) {
                    Logger.LogFormat(LogLevel.Info, "Found product {0}", productCode);
                }

                if (products.Count > 0) {
                    if (entry.Value.OnboardFamilies.All(x => x.Equals(onboardFamily, StringComparison.OrdinalIgnoreCase))) {
                        Logger.LogFormat(LogLevel.Info, "Found compatible products of upgrade code {0}", entry.Key);
                        foundCompatible = true;
                    } else {
                        Logger.LogFormat(LogLevel.Info, "Found INCOMPATIBLE products of upgrade code {0}", entry.Key);
                        foundIncompatible = true;
                    }
                }
            }
        }
    }
}
