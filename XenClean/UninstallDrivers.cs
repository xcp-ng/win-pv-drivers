using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using XenDriverUtils;

namespace XenClean {
    internal static class UninstallDrivers {
        static readonly List<string> WantedCatalogNames = XenDeviceInfo.KnownDevices.Keys.Select(x => $"{x}.cat").ToList();

        public static void Execute() {
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var infdir = Path.Combine(windir, "inf");
            foreach (var oemInfPath in Directory.EnumerateFiles(infdir, "oem*.inf", SearchOption.TopDirectoryOnly)) {
                if (!".inf".Equals(Path.GetExtension(oemInfPath), StringComparison.OrdinalIgnoreCase)) {
                    // netfx bug when using asterisks
                    continue;
                }
                try {
                    using var infFile = InfFile.Open(oemInfPath, null, INF_STYLE.INF_STYLE_WIN4, out _);
                    var infCatalog = infFile.GetStringField("Version", "CatalogFile", 1);
                    if (WantedCatalogNames.Contains(infCatalog, StringComparer.OrdinalIgnoreCase)) {
                        Logger.Log($"Found driver: {oemInfPath}");
                    } else {
                        continue;
                    }
                } catch (Exception ex) {
                    Logger.Log($"Cannot parse {oemInfPath}: {ex.Message}");
                }
                var oemInfName = Path.GetFileName(oemInfPath);
                try {
                    DriverUtils.UninstallDriver(oemInfName);
                } catch (Exception ex) {
                    Logger.Log($"Cannot uninstall driver {oemInfName}: {ex.Message}");
                }
            }
        }
    }
}
