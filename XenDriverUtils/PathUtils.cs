using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace XenDriverUtils {
    public static class PathUtils {
        const string AdminSd = "O:BAG:BAD:(A;;GA;;;BA)(A;;GA;;;SY)";

        public static DirectoryInfo CreateSecureTempDirectory() {
            var tempRoot = Path.GetTempPath();
            for (int attempt = 0; attempt < 100; attempt++) {
                var randomPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("D"));

                if (!PInvoke.ConvertStringSecurityDescriptorToSecurityDescriptor(AdminSd, PInvoke.SDDL_REVISION_1, out var sd)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                bool success;
                unsafe {
                    var sa = new SECURITY_ATTRIBUTES() {
                        nLength = (uint)Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                        lpSecurityDescriptor = sd.Value,
                        bInheritHandle = false,
                    };
                    success = PInvoke.CreateDirectory(randomPath, sa);
                }

                if (success) {
                    return new DirectoryInfo(randomPath);
                }

                var err = Marshal.GetLastWin32Error();
                if (err != (int)WIN32_ERROR.ERROR_ALREADY_EXISTS) {
                    throw new Win32Exception(err, $"Creating {randomPath} failed");
                }
            }
            throw new IOException("Tried too many times without creating new directory");
        }
    }
}
