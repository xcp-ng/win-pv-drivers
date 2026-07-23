using System.ComponentModel;
using XenPlus.XenIface;

namespace XenPlus.VolumeInfo;

static class VbdStore {
    internal static List<(string vbdKey, uint vbdNumber)> GetVbds(XenIfaceHandle h) {
        var result = new List<(string vbdKey, uint vbdNumber)>();
        var vbdKeys = h.StoreTryDirectory("device/vbd");
        if (vbdKeys == null) {
            return result;
        }
        foreach (var vbdKey in vbdKeys) {
            var vbdNumber = h.StoreTryReadStrict(StoreUtils.PathJoin("device/vbd", vbdKey, "virtual-device"));
            if (uint.TryParse(vbdNumber, out var parsed)) {
                result.Add((vbdKey, parsed));
            }
        }
        return result;
    }

    internal static (string prefix, uint id) VbdNumberToTargetId(uint vbdNumber) {
        if ((vbdNumber & (1u << 28)) != 0) {
            ArgumentOutOfRangeException.ThrowIfNotEqual(vbdNumber & 0xeff000ffu, 0u);
            return ("xvd", (vbdNumber & ((1u << 20) - 1)) >> 8);
        }
        switch (vbdNumber >> 8) {
            case 202:
                ArgumentOutOfRangeException.ThrowIfNotEqual(vbdNumber & 0xf, 0u);
                return ("xvd", (vbdNumber & 0xf0) >> 4);
            case 8:
                ArgumentOutOfRangeException.ThrowIfNotEqual(vbdNumber & 0xf, 0u);
                return ("sd", (vbdNumber & 0xf0) >> 4);
            case 3:
                ArgumentOutOfRangeException.ThrowIfNotEqual(vbdNumber & 0x3f, 0u);
                return ("hd", (vbdNumber & 0xc0) >> 6);
            case 22:
                ArgumentOutOfRangeException.ThrowIfNotEqual(vbdNumber & 0x3f, 0u);
                return ("hd", ((vbdNumber & 0xc0) >> 6) + 2);
            default:
                throw new ArgumentException($"VBD number {vbdNumber} is not supported");
        }
    }

    internal static string FormatTargetName(string prefix, uint id) {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz";
        var result = "";
        while (id >= 26) {
            result += alphabet[(int)(id % 26)];
            id = (id / 26) - 1;
        }
        result += alphabet[(int)id];
        var suffix = result.ToCharArray();
        Array.Reverse(suffix);
        return prefix + new string(suffix);
    }
}
