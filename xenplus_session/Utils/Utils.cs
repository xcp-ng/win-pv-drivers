using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace XenPlus;

static class Utils {
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
}
