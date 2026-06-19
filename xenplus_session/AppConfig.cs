using Microsoft.Win32;

namespace XenPlus;

sealed class AppConfig {
    const uint _assert_VendorName = VersionInfo.VendorName == "" ? -1 : 0;
    const string ConfigKeyName = $"SOFTWARE\\{VersionInfo.VendorName}\\XenPlus";

    bool _showTrayIcon = true;
    public bool ShowTrayIcon {
        get {
            return _showTrayIcon;
        }
        set {
            WriteShowTrayIcon(value);
            _showTrayIcon = value;
        }
    }
    void ReadShowTrayIcon(RegistryKey configKey) {
        try {
            _showTrayIcon = GetConfigValue<int>(configKey, nameof(ShowTrayIcon), _showTrayIcon) != 0;
        } catch {
        }
    }
    void WriteShowTrayIcon(bool value) {
        using var configKey = GetConfigKey(true) ?? throw new NullReferenceException(nameof(GetConfigKey));
        configKey.SetValue(nameof(ShowTrayIcon), value ? 1 : 0, RegistryValueKind.DWord);
    }

    static RegistryKey? GetConfigKey(bool writing) {
        if (writing) {
            return Registry.CurrentUser.CreateSubKey(ConfigKeyName, true);
        } else {
            return Registry.CurrentUser.OpenSubKey(ConfigKeyName, false);
        }
    }

    static T GetConfigValue<T>(RegistryKey key, string name, object? defaultValue = null) {
        var result = key.GetValue(name, defaultValue) ?? throw new NullReferenceException(name);
        return (T)result;
    }

    public AppConfig() {
        using var configKey = GetConfigKey(false);
        if (configKey == null) {
            return;
        }
        ReadShowTrayIcon(configKey);
    }
}
