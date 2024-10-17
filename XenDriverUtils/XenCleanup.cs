using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;

namespace XenDriverUtils {
    public class XenCleanup {
        private static readonly List<Guid> ClassKeyList = new() {
            PInvoke.GUID_DEVCLASS_HDC,
            PInvoke.GUID_DEVCLASS_SYSTEM,
        };

        private static readonly List<string> FilterValueList = new() {
            "LowerFilters",
            "UpperFilters",
        };

        private static readonly List<string> FilterNameList = new() {
            "xenfilt",
            "scsifilt",
        };

        public static void XenbusCleanup() {
            using var classKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class", true);
            if (classKey == null) {
                return;
            }
            foreach (var classGuid in ClassKeyList) {
                using var classSubkey = classKey.OpenSubKey(classGuid.ToString("B"), true);
                if (classSubkey == null) {
                    continue;
                }
                foreach (var filterValue in FilterValueList) {
                    try {
                        if (classSubkey.GetValueKind(filterValue) == RegistryValueKind.MultiString) {
                            var filters = (string[])classSubkey.GetValue(filterValue);
                            Logger.Log($"Class filters for {classGuid}: {string.Join(",", filters)}");
                            var newFilters = filters.Where(x => !FilterNameList.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
                            Logger.Log($"New filters for {classGuid}: {string.Join(",", newFilters)}");
                            classSubkey.SetValue(filterValue, newFilters, RegistryValueKind.MultiString);
                        }
                    } catch {
                    }
                }
            }
        }

        private static readonly List<string> XenfiltParametersToDelete = new() {
            "ActiveDeviceID",
            "ActiveInstanceID",
            "ActiveLocationInformation",
        };

        public static void XenfiltReset() {
            using var paramKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\xenfilt\\Parameters", true);
            if (paramKey == null) {
                return;
            }
            foreach (var paramName in XenfiltParametersToDelete) {
                try {
                    paramKey.DeleteValue(paramName);
                    Logger.Log($"Deleted xenfilt parameter {paramName}");
                } catch {
                }
            }
        }
    }
}
