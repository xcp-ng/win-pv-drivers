using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace XNInstCA {
    public class DriverData {
        public string DriverName { get; set; }
        public string InfPath { get; set; }
    }

    internal static class DriverUtils {
        public static DriverData GetDriverData(Session session) {
            if (!session.CustomActionData.TryGetValue("Driver", out var driverName)) return null;
            if (!session.CustomActionData.TryGetValue("Inf", out var infPath)) return null;
            return new DriverData() {
                DriverName = driverName,
                InfPath = infPath,
            };
        }
    }
}
