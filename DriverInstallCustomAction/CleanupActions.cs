using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace XNInstCA {
    public class CleanupActions {
        private static readonly List<Guid> ClassKeyList = new() {
            PInvoke.GUID_DEVCLASS_HDC,
            PInvoke.GUID_DEVCLASS_SYSTEM,
        };

        private static readonly List<string> FilterValueList = new() {
            "UpperFilters",
        };

        [CustomAction]
        public static ActionResult XenbusCleanup(Session session) {
            using var classKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class", true);
            if (classKey == null) {
                return ActionResult.Success;
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
                            session.Log($"Class filters for {classGuid}: {string.Join(",", filters)}");
                            var newFilters = filters.Where(x => !"xenfilt".Equals(x, StringComparison.OrdinalIgnoreCase)).ToArray();
                            session.Log($"New filters for {classGuid}: {string.Join(",", newFilters)}");
                            classSubkey.SetValue(filterValue, newFilters, RegistryValueKind.MultiString);
                        }
                    } catch {
                    }
                }
            }
            return ActionResult.Success;
        }
    }
}
