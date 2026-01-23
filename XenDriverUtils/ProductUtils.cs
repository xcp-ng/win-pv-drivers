using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace XenDriverUtils {
    public static class ProductUtils {
        // for synchronizing MsiEnumRelatedProducts
        static readonly object MsiLock = new();
        // Guid.Empty.ToString("B").Length + 1
        const int GuidLength = 39;

        public static List<string> EnumerateProducts(string upgradeCode) {
            var result = new List<string>();
            var buffer = new char[GuidLength];

            lock (MsiLock) {
                for (uint i = 0; ; i++) {
                    var err = PInvoke.MsiEnumRelatedProducts(upgradeCode, i, buffer);
                    if (err == (uint)WIN32_ERROR.ERROR_SUCCESS) {
                        var len = Array.IndexOf(buffer, '\0');
                        if (len < 0) {
                            len = buffer.Length;
                        }
                        result.Add(new string(buffer, 0, len));
                    } else if (err == (uint)WIN32_ERROR.ERROR_NO_MORE_ITEMS) {
                        break;
                    } else {
                        throw new Win32Exception(unchecked((int)err), "MsiEnumRelatedProducts failed");
                    }
                }
            }

            return result;
        }
    }
}
