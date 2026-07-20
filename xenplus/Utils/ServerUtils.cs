using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;

namespace XenPlus;

static class ServerUtils {
    public static List<string> ParseMultiString<T>(ReadOnlySpan<T> buf, Func<ReadOnlySpan<T>, string> ctor)
    where T : struct, IEquatable<T> {
        var strings = new List<string>();
        if (buf.Length == 0) {
            return strings;
        }
        for (int i = 0; i < buf.Length; i++) {
            if (default(T).Equals(buf[i])) {
                break;
            }
            int start = i;
            while (i < buf.Length && !default(T).Equals(buf[i])) {
                i++;
            }
            strings.Add(ctor(buf[start..i]));
        }
        return strings;
    }

    public static List<string> ParseMultiString(ReadOnlySpan<char> buf) {
        return ParseMultiString(buf, static x => new string(x));
    }

    public static int HresultFromWin32(int x) {
        return x <= 0 ? (HRESULT)x : (HRESULT)unchecked(
            ((uint)x & 0x0000FFFF) |
            ((uint)FACILITY_CODE.FACILITY_WIN32 << 16) |
            0x80000000u);
    }

    /// <summary>
    /// like the cooked version of Get-PackageVersion
    /// </summary>
    public static string NormalizeVersion(int version) {
        return version >= 0 ? version.ToString() : "";
    }
}
