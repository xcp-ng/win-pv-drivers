using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;

namespace XenPlus;

/// <summary>
/// The data type of a single policy layer (per-machine or per-user).
/// </summary>
public interface IPolicyInstance<T> {
    abstract static T? LoadPolicy(RegistryKey key);

    public static bool? ReadBool(RegistryKey key, string valueName) {
        try {
            var value = key.GetValue(valueName);
            if (value == null) {
                return null;
            }
            return value switch {
                int x => x != 0,
                long x => x != 0,
                _ => null,
            };
        } catch {
            return null;
        }
    }
}

public sealed class PolicyStore<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>
    where T : IPolicyInstance<T> {
    readonly Lazy<List<T?>> _policies;

    public PolicyStore(
        string vendorKey,
        string category,
        params RegistryKey[] roots) {
        ArgumentException.ThrowIfNullOrEmpty(vendorKey);
        ArgumentException.ThrowIfNullOrEmpty(category);
        _policies = new(() => roots.Select(root => {
            RegistryKey? key = null;
            try {
                key = root.OpenSubKey($"SOFTWARE\\Policies\\{vendorKey}\\{category}");
            } catch {
            }
            if (key == null) {
                return default;
            }

            using (key) {
                return T.LoadPolicy(key);
            }
        }).ToList());
    }

    public List<T?> Policies => _policies.Value;

    public U? Get<U>(Func<T, U?> selector) {
        var policies = Policies;
        if (policies == null) {
            return default;
        }
        return policies
            .OfType<T>()
            .Select(selector)
            .FirstOrDefault();
    }
}
