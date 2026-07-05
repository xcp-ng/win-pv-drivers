using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Options;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using XenPlus;
using XenPlus.Features;
using XenPlus.VifConfigure;
using XenPlus.XenIface;

sealed class VifConfigureOptions {
    public bool Enabled { get; set; } = true;
    public bool AllowConfigureNonVifs { get; set; } = true;
    [Range(100, 3_600_000)]
    public int CommandTimeoutMilliseconds { get; set; } = 5000;
}

[OptionsValidator]
partial class ValidateVifConfigureOptions : IValidateOptions<VifConfigureOptions> {
}

sealed class VifConfigureFeature(
    IHostLifetime _hostLifetime,
    IOptionsMonitor<VifConfigureOptions> _options,
    XenIfaceSource _xi,
    ILogger<VifConfigureFeature> _logger) : FeatureBase(_hostLifetime, _logger) {
    const string FeatureKey = "control/feature-static-ip-setting";
    const string VifConfigRoot = "xenserver/device/vif";
    static readonly string NetshPath = Path.Combine(Environment.SystemDirectory, "netsh.exe");
    readonly ReferenceCount _active = new();
    readonly SemaphoreSlim _lock = new(1, 1);
    XenIfaceWatch? _watch = null;

    static List<(string vifKey, string vifMac)> FindVifMacs(XenIfaceHandle h) {
        List<(string vifKey, string vifMac)> result = [];
        var vifKeys = h.StoreDirectory("device/vif");
        foreach (var vifKey in vifKeys.Where(x => !string.IsNullOrEmpty(x))) {
            string? vifMac = null;
            try {
                vifMac = h.StoreReadStrict($"device/vif/{vifKey}/mac");
            } catch {
            }
            if (vifMac != null) {
                result.Add((vifKey, vifMac));
            }
        }
        return result;
    }

    HashSet<VifConfiguration> ParseVifConfigurations() {
        var configs = new HashSet<VifConfiguration>(new VifConfigurationEqualityComparer());

        // annoyingly, Windows watch doesn't expose the real triggered path, so it's up to us to scan ourselves
        using (var h = _xi.Lock()) {
            var allowConfigureNonVifs = _options.CurrentValue.AllowConfigureNonVifs;
            // don't bother scanning if allowConfigureNonVifs is true
            var vifMacs = allowConfigureNonVifs ? [] : FindVifMacs(h);
            foreach (var vc in h.StoreDirectory(VifConfigRoot).Select(key => $"{VifConfigRoot}/{key}")) {
                try {
                    var mac = VifStore.VifConfigurationGetMac(h, vc);
                    if (!allowConfigureNonVifs &&
                        !vifMacs.Any(x => x.vifMac.Equals(mac, StringComparison.OrdinalIgnoreCase))) {
                        _logger.LogDebug(
                            "Denied configuration attempt at '{}' since AllowConfigureNonVifs is false and MAC '{}' " +
                            "does not match the list of VIFs",
                            vc,
                            mac);
                        continue;
                    }
                    try {
                        if (VifStore.ParseVifConfigurationIPv4(h, vc, mac) is VifConfigurationIPv4 configv4) {
                            configs.Add(configv4);
                        }
                    } catch (Exception ex) {
                        _logger.LogWarning(ex, "Cannot parse IPv4 config update for '{}' (MAC '{}')", vc, mac);
                    }
                    try {
                        if (VifStore.ParseVifConfigurationIPv6(h, vc, mac) is VifConfigurationIPv6 configv6) {
                            configs.Add(configv6);
                        }
                    } catch (Exception ex) {
                        _logger.LogWarning(ex, "Cannot parse IPv6 config update for '{}' (MAC '{}')", vc, mac);
                    }
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Cannot process VIF config updates for '{}'", vc);
                }
            }
        }

        return configs;
    }

    static async Task ThrowIfCommandFailed(
        Process process,
        string fileName,
        IReadOnlyList<string> args,
        Task<string> stdoutTask,
        Task<string> stderrTask,
        CancellationToken ct) {
        if (process.ExitCode != 0) {
            var command = $"{fileName} {string.Join(' ', args)}";
            var stdout = "";
            var stderr = "";
            try {
                stdout = await stdoutTask.WaitAsync(ct);
                stderr = await stderrTask.WaitAsync(ct);
            } catch {
            }
            var output = $"{stdout}{stderr}";
            throw !string.IsNullOrEmpty(output)
                ? new Exception($"Command '{command}' failed with exit code {process.ExitCode}:\n\n{output}")
                : new Exception($"Command '{command}' failed with exit code {process.ExitCode}");
        }
    }

    async Task ApplyVifConfiguration(
        MibIfTable2SafeHandle mibIfTable,
        MibUnicastIpAddressTableSafeHandle mibIPTable,
        MibIpForwardTable2SafeHandle mibRouteTable,
        VifConfiguration config,
        CancellationToken ct) {
        if (mibIfTable.FindIfaceRow(config.Mac) is not MIB_IF_ROW2 mibIf) {
            _logger.LogWarning(
                "Cannot find matching network interface for '{}' (MAC '{}')",
                config.StorePath,
                config.Mac);
            return;
        }
        _logger.LogDebug(
            "Identified config target iface '{}' (LUID {}, index {}, guid {})",
            mibIf.Alias.ToString(),
            mibIf.InterfaceLuid.Value,
            mibIf.InterfaceIndex,
            mibIf.InterfaceGuid);

        // The IPHelper API for IP configuration is severely lacking. For example, IPv4 DHCP config is not possible.
        // The IPv6 API doesn't even have IP release/renew capabilities. So netsh it is...

        List<(string fileName, List<string> arguments)> commands = [];

        var interfaceIndex = mibIf.InterfaceIndex.ToString();
        if (config is VifConfigurationIPv4Dhcp) {
            if (!mibIPTable.HasDhcpAddress(mibIf.InterfaceIndex, ADDRESS_FAMILY.AF_INET)) {
                commands.Add((NetshPath, ["interface", "ipv4", "set", "address", interfaceIndex, "source=dhcp"]));
            }
        } else if (config is VifConfigurationIPv4Static staticv4) {
            var address = staticv4.Address[0];
            commands.Add((NetshPath, [
                "interface",
                "ipv4",
                "set",
                "address",
                interfaceIndex,
                "source=static",
                $"address={address.Address}/{address.Prefix}",
                $"gateway={staticv4.Gateway?.ToString() ?? "none"}",
            ]));
        } else if (config is VifConfigurationIPv6Autoconf) {
            foreach (var address in mibIPTable.GetManualUnicastAddresses(
                mibIf.InterfaceIndex,
                ADDRESS_FAMILY.AF_INET6)) {
                commands.Add((NetshPath, [
                    "interface",
                    "ipv6",
                    "delete",
                    "address",
                    interfaceIndex,
                    address.Address.ToString()
                ]));
            }
        } else if (config is VifConfigurationIPv6Static staticv6) {
            var address = staticv6.Address[0];
            foreach (var existing in mibIPTable.GetManualUnicastAddresses(
                mibIf.InterfaceIndex,
                ADDRESS_FAMILY.AF_INET6)) {
                if (existing.Address.Equals(address.Address) && existing.Prefix == address.Prefix) {
                    continue;
                }
                commands.Add((NetshPath, [
                    "interface",
                    "ipv6",
                    "delete",
                    "address",
                    interfaceIndex,
                    existing.Address.ToString()
                ]));
            }
            if (!mibIPTable.HasUnicastAddress(mibIf.InterfaceIndex, address)) {
                commands.Add((NetshPath, [
                    "interface",
                    "ipv6",
                    "add",
                    "address",
                    interfaceIndex,
                    $"{address.Address}/{address.Prefix}",
                ]));
            }
            if (staticv6.Gateway is IPAddress gateway &&
                !mibRouteTable.HasDefaultRoute(mibIf.InterfaceIndex, gateway)) {
                commands.Add((NetshPath, [
                    "interface",
                    "ipv6",
                    "add",
                    "route",
                    "::/0",
                    interfaceIndex,
                    gateway.ToString(),
                ]));
            }
        }

        if (commands.Count > 0) {
            DebugLogDebug(
                "Running {} netsh script:\n{}",
                commands.Count,
                string.Join("\n", commands.Select(cmd => $"{cmd.fileName} {string.Join(' ', cmd.arguments)}")));
            foreach (var (fileName, arguments) in commands) {
                var psi = new ProcessStartInfo() {
                    FileName = fileName,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (var arg in arguments) {
                    psi.ArgumentList.Add(arg);
                }

                using var process = Process.Start(psi) ??
                    throw new NullReferenceException("command process not started");
                process.StandardInput.Close();
                var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
                var stderrTask = process.StandardError.ReadToEndAsync(ct);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(_options.CurrentValue.CommandTimeoutMilliseconds);

                try {
                    await process.WaitForExitAsync(timeout.Token);
                } catch (OperationCanceledException) {
                    process.Kill();
                    process.WaitForExit();
                }
                await ThrowIfCommandFailed(process, fileName, arguments, stdoutTask, stderrTask, ct);
            }
        }
    }

    void ReportFeature(object? sender, XenIfaceResumedEventArgs args) {
        try {
            using var h = _xi.Lock();

            h.StoreWrite(FeatureKey, "1");
        } catch (Exception ex) {
            _logger.LogDebug(ex, "{} report error", nameof(VifConfigureFeature));
        }
    }

    void Cleanup() {
        try {
            using var h = _xi.Lock();

            h.StoreRemove(FeatureKey);
        } catch (Exception ex) {
            _logger.LogDebug(ex, "{} cleanup error", nameof(VifConfigureFeature));
        }
    }

    protected override async Task ExecuteFeatureAsync(CancellationToken stoppingToken) {
        if (!_options.CurrentValue.Enabled) {
            return;
        }

        async void onWatch(object? sender, XenIfaceWatchEventArgs args) {
            using var lifetime = _active.TryEnterScope();
            if (lifetime == null) {
                return;
            }
            try {
                using var scope = await _lock.EnterScopeAsync(stoppingToken);
                using var mibIfTable = MibIfTable2SafeHandle.GetIfTable2();
                using var mibIPTable = MibUnicastIpAddressTableSafeHandle.GetUnicastIpAddressTable(
                    ADDRESS_FAMILY.AF_UNSPEC);
                using var mibRouteTable = MibIpForwardTable2SafeHandle.GetIpForwardTable2(ADDRESS_FAMILY.AF_UNSPEC);

                foreach (var config in ParseVifConfigurations()) {
                    _logger.LogDebug("Applying config update for '{}' (MAC '{}')", config.StorePath, config.Mac);
                    try {
                        await ApplyVifConfiguration(mibIfTable, mibIPTable, mibRouteTable, config, stoppingToken);
                    } catch (Exception ex) {
                        _logger.LogWarning(
                            ex,
                            "Cannot apply VIF config update for '{}' (MAC '{}')",
                            config.StorePath,
                            config.Mac);
                    }
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Cannot process VIF config update");
            }
        }

        bool registered = false;
        bool watched = false;
        try {
            _xi.Resumed += ReportFeature;
            registered = true;
            _watch = _xi.WatchAdd(VifConfigRoot);
            _watch.WatchTriggered += onWatch;
            watched = true;

            ReportFeature(null, new());
            await Task.Delay(Timeout.Infinite, stoppingToken);
        } finally {
            await _active.RundownAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);
            if (registered) {
                _xi.Resumed -= ReportFeature;
            }
            if (watched) {
                _watch!.WatchTriggered -= onWatch;
            }
            _watch?.Dispose();
            _watch = null;
            Cleanup();
        }
    }
}
