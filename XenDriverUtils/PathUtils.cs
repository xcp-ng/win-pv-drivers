using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace XenDriverUtils {
    public static class PathUtils {
        /// <summary>
        /// Protected SDDL granting full access only to Administrators, SYSTEM and (when not elevated) the current user.
        /// </summary>
        /// <param name="container">Make the ACEs inheritable by children (for directories).</param>
        public static string GetSecureSddl(bool container) {
            var flags = container ? "OICI" : "";
            using var identity = WindowsIdentity.GetCurrent();
            var isAdmin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            if (isAdmin) {
                return $"O:BAG:BAD:P(A;{flags};GA;;;BA)(A;{flags};GA;;;SY)";
            } else {
                var user = identity.User?.ToString() ?? throw new IOException("Cannot determine current user");
                return $"O:{user}G:{user}D:P(A;{flags};GA;;;{user})(A;{flags};GA;;;BA)(A;{flags};GA;;;SY)";
            }
        }

        public static DirectoryInfo CreateSecureTempDirectory() {
            var tempRoot = Path.GetTempPath();

            if (!PInvoke.ConvertStringSecurityDescriptorToSecurityDescriptor(
                GetSecureSddl(container: true),
                PInvoke.SDDL_REVISION_1,
                out var sd)) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            try {
                SECURITY_ATTRIBUTES sa;
                unsafe {
                    sa = new SECURITY_ATTRIBUTES() {
                        nLength = (uint)Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                        lpSecurityDescriptor = sd.Value,
                        bInheritHandle = false,
                    };
                }

                for (int attempt = 0; attempt < 100; attempt++) {
                    var randomPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("D"));

                    bool success;
                    unsafe {
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
            } finally {
                unsafe {
                    PInvoke.LocalFree((HLOCAL)sd.Value);
                }
            }
        }
    }
}
