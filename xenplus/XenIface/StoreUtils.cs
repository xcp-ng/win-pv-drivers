using System.ComponentModel;
using Windows.Win32.Foundation;

namespace XenPlus.XenIface;

static class StoreUtils {
    public static string PathJoin(string? a, string b) {
        ArgumentNullException.ThrowIfNull(b);
        if (b.StartsWith('/')) {
            throw new ArgumentException("Path suffix cannot be absolute");
        }
        if (a?.EndsWith('/') ?? false) {
            return a + b;
        }
        return a + '/' + b;
    }

    public static bool ExceptionIsStoreNotFound(Exception ex) {
        return ex is Win32Exception wex && wex.NativeErrorCode == (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND;
    }
}
