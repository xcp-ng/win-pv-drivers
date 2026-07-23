namespace XenPlus.XenIface;

static class StoreUtils {
    public static string PathJoin(string? a, params string[] bs) {
        string result = a ?? "";
        ArgumentNullException.ThrowIfNull(bs);
        foreach (var b in bs) {
            ArgumentNullException.ThrowIfNull(b);
            if (b.StartsWith('/')) {
                throw new ArgumentException("Path suffix cannot be absolute");
            }
            if (result.EndsWith('/')) {
                result += b;
            } else {
                result += "/" + b;
            }
        }
        return result;
    }
}
