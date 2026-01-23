using Microsoft.Win32;
using System;
using System.IO;
using XenDriverUtils;

namespace XenClean {
    static class DiagnosticUtils {
        const string HexAlphabet = "0123456789ABCDEF";

        static string ToHexString(byte[] value) {
            var result = new char[value.Length * 2];
            for (int i = 0; i < value.Length; i++) {
                result[i * 2] = HexAlphabet[value[i] >> 4];
                result[i * 2 + 1] = HexAlphabet[value[i] & 0xf];
            }

            return new string(result);
        }

        public static void LogRegistryKey(RegistryKey root, string keyName) {
            if (root == null) {
                return;
            }

            var keyPath = $"{root.Name}\\{keyName}";
            using var section = new LogSection(keyPath);

            RegistryKey key;
            try {
                key = root.OpenSubKey(keyName, false);
            } catch (Exception ex) {
                Logger.LogFormat(LogLevel.Info, "Error when opening key {0} for logging: {1} {2}", keyPath, ex.HResult, ex.Message);
                return;
            }
            using (key) {
                if (key == null) {
                    return;
                }

                foreach (var name in key.GetValueNames()) {
                    RegistryValueKind kind;
                    object value;
                    var displayName = string.IsNullOrEmpty(name) ? "(default)" : name;

                    try {
                        kind = key.GetValueKind(name);
                        value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    } catch (Exception ex) {
                        Logger.LogFormat(LogLevel.Info, "Error when reading value {0}: {1} {2}", displayName, ex.HResult, ex.Message);
                        continue;
                    }

                    if (value == null) {
                        Logger.LogFormat(LogLevel.Info, "{0}=({1})(null)", displayName, kind);
                    } else if (value is string[] stringArray) {
                        Logger.LogFormat(LogLevel.Info, "{0}=({1}){2}", displayName, kind, string.Join("|", stringArray));
                    } else if (value is byte[] byteArray) {
                        Logger.LogFormat(LogLevel.Info, "{0}=({1}){2}", displayName, kind, ToHexString(byteArray));
                    } else {
                        Logger.LogFormat(LogLevel.Info, "{0}=({1}){2}", displayName, kind, value);
                    }
                }

                foreach (var subkeyName in key.GetSubKeyNames()) {
                    LogRegistryKey(key, subkeyName);
                }
            }
        }

        static readonly string[] ServicesToLog = {
            "XEN", "xenbus", "xencons", "xenfilt", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd", "Tcpip", "Tcpip6"
        };

        public static void LogSystemState() {
            var pnputil = Path.Combine(Environment.SystemDirectory, "pnputil.exe");

            using (ProcessRedirector.LogCommand(pnputil, "/enum-drivers", TimeSpan.FromMinutes(1), LogLevel.Trace)) { }
            if (Environment.OSVersion.Version >= new Version(10, 0, 18362)) {
                using (ProcessRedirector.LogCommand(pnputil, "/enum-devices /relations", TimeSpan.FromMinutes(2), LogLevel.Trace)) { }
            }

            LogRegistryKey(Registry.LocalMachine, "SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e96a-e325-11ce-bfc1-08002be10318}");
            LogRegistryKey(Registry.LocalMachine, "SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e97d-e325-11ce-bfc1-08002be10318}");

            foreach (var service in ServicesToLog) {
                LogRegistryKey(Registry.LocalMachine, $"SYSTEM\\CurrentControlSet\\Services\\{service}");
            }

            LogRegistryKey(Registry.LocalMachine, "SOFTWARE\\XenOffboard");
        }
    }
}
