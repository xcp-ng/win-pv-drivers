using Microsoft.Win32;

namespace XenPlus;

sealed class SessionPolicyInstance : IPolicyInstance<SessionPolicyInstance> {
    public const string Category = "XenPlus";
    public required bool? HideTrayIcon { get; init; }

    public static SessionPolicyInstance? LoadPolicy(RegistryKey key) {
        return new() {
            HideTrayIcon = IPolicyInstance<SessionPolicyInstance>.ReadBool(key, nameof(HideTrayIcon)),
        };
    }
}

sealed class SessionPolicyService {
    readonly PolicyStore<SessionPolicyInstance> _policy = new(
        VersionInfo.VendorKey,
        SessionPolicyInstance.Category,
        Registry.LocalMachine,
        Registry.CurrentUser);

    public bool HideTrayIcon => _policy.Get(p => p.HideTrayIcon) ?? false;
}
