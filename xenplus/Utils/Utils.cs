using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;

namespace XenPlus;

static class Utils {
    public static List<string> ParseMultiString<T>(T[] buf, Func<T[], int, int, string> ctor)
    where T : struct, IEquatable<T> {
        var strings = new List<string>();
        if (buf == null || buf.Length == 0) {
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
            strings.Add(ctor(buf, start, i - start));
        }
        return strings;
    }

    public static List<string> ParseMultiString(char[] buf) {
        return ParseMultiString(buf, static (x, y, z) => new string(x, y, z));
    }

    public static void CheckConfigret(CONFIGRET cr) {
        if (cr != CONFIGRET.CR_SUCCESS) {
            throw new Win32Exception(unchecked((int)PInvoke.CM_MapCrToWin32Err(cr, (uint)WIN32_ERROR.ERROR_GEN_FAILURE)));
        }
    }

    public static int HresultFromWin32(int x) {
        return x <= 0 ? (HRESULT)x : (HRESULT)unchecked(
            ((uint)x & 0x0000FFFF) |
            ((uint)FACILITY_CODE.FACILITY_WIN32 << 16) |
            0x80000000u);
    }

    public static void Assert(
        [DoesNotReturnIf(false)] bool condition,
        string? message,
        string prefix = "assertion failed: ") {
        if (!condition) {
            Environment.FailFast(prefix + message);
        }
    }

#pragma warning disable IDE0280 // Use 'nameof'
    [Conditional("DEBUG")]
    public static void DebugAssert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression("condition")] string? message = null) {
        Assert(condition, message);
    }

    [Conditional("TRACE")]
    public static void TraceAssert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression("condition")] string? message = null) {
        Assert(condition, message);
    }

    public static T Unwrap<T>(
        object? value,
        [CallerArgumentExpression("value")] string? message = null)
        where T : class {
        var castValue = value as T;
        Assert(castValue != null, message, "value is null: ");
        return castValue!;
    }
#pragma warning restore IDE0280 // Use 'nameof'

    /// <summary>
    /// like the cooked version of Get-PackageVersion
    /// </summary>
    public static string NormalizeVersion(int version) {
        return version >= 0 ? version.ToString() : "";
    }
}
