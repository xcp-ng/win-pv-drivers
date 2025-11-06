using System;

namespace XenClean {
    internal class Utils {
        public static bool IsSafeMode() {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAFEBOOT_OPTION"));
        }
    }
}
