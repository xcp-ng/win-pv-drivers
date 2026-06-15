using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace XenPlus;

public static class Check {
    public static void Assert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? message = null,
        string prefix = "assertion failed: ") {
        if (!condition) {
            Environment.FailFast(prefix + message);
        }
    }

    [Conditional("DEBUG")]
    public static void DebugAssert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? message = null) {
        Assert(condition, message);
    }

    [Conditional("TRACE")]
    public static void TraceAssert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? message = null) {
        Assert(condition, message);
    }

    public static T Unwrap<T>(
        object? value,
        [CallerArgumentExpression(nameof(value))] string? message = null)
        where T : class {
        var castValue = value as T;
        Assert(castValue != null, message, "value is null: ");
        return castValue!;
    }
}
