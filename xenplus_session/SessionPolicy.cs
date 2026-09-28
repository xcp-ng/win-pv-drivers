using Microsoft.Win32;

namespace XenPlus;

sealed class SessionPolicyInstance : IPolicyInstance<SessionPolicyInstance> {
    public const string Category = "XenPlus";
    public required bool? ShowTrayIcon { get; init; }

    public static SessionPolicyInstance? LoadPolicy(RegistryKey key) {
        return new() {
            ShowTrayIcon = IPolicyInstance<SessionPolicyInstance>.ReadBool(key, nameof(ShowTrayIcon)),
        };
    }
}

sealed class SessionPolicyService {
    readonly PolicyStore<SessionPolicyInstance> _policy = new(
        RequiredConstants.VendorKey,
        SessionPolicyInstance.Category,
        Registry.LocalMachine,
        Registry.CurrentUser);

    public bool ShowTrayIcon => _policy.Get(p => p.ShowTrayIcon) ?? true;
    public void Refresh() {
        _policy.Refresh();
    }
}
