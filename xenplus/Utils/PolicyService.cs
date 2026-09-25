using Microsoft.Win32;

namespace XenPlus;

sealed class PolicyInstance : IPolicyInstance<PolicyInstance> {
    public const string Category = "XenPlus";
    public required bool? DisableRemoteControl { get; init; }

    public static PolicyInstance? LoadPolicy(RegistryKey key) {
        return new() {
            DisableRemoteControl = IPolicyInstance<PolicyInstance>.ReadBool(key, nameof(DisableRemoteControl)),
        };
    }
}

sealed class PolicyService {
    readonly PolicyStore<PolicyInstance> _policy = new(
        VersionInfo.VendorKey,
        PolicyInstance.Category,
        Registry.LocalMachine);

    public bool DisableRemoteControl => _policy.Get(p => p.DisableRemoteControl) ?? false;
}
