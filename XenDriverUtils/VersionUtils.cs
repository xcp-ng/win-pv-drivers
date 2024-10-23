using System;
using Windows.Win32.System.SystemInformation;

namespace XenDriverUtils {
    public static class VersionUtils {
        // Unlike Environment.OSVersion, RtlGetVersion won't lie to us regardless of host manifest.
        public static Version GetWindowsVersion() {
            var versionInfo = new OSVERSIONINFOW();
            var ret = Windows.Wdk.PInvoke.RtlGetVersion(ref versionInfo);
            if (ret < 0) {
                throw new Exception($"RtlGetVersion failed with NTSTATUS {ret}");
            }
            return new Version(
                (int)versionInfo.dwMajorVersion,
                (int)versionInfo.dwMinorVersion,
                (int)versionInfo.dwBuildNumber);
        }
    }
}
