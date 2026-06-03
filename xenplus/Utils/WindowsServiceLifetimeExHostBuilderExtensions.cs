// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// From src\libraries\Microsoft.Extensions.Hosting.WindowsServices\src\WindowsServiceLifetimeHostBuilderExtensions.cs

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;

namespace XenPlus;

/// <summary>
/// Extension methods for setting up WindowsServiceLifetimeEx.
/// </summary>
static class WindowsServiceLifetimeExHostBuilderExtensions {
    /// <summary>
    /// Sets the host lifetime to <see cref="WindowsServiceLifetimeEx"/> and enables logging to the event log with
    /// the application name as the default source name.
    /// </summary>
    /// <remarks>
    /// This is context aware and will only activate if it detects the process is running as a Windows Service.
    /// </remarks>
    /// <param name="hostBuilder">The <see cref="IHostBuilder"/> to operate on.</param>
    /// <returns>The <paramref name="hostBuilder"/> instance for chaining.</returns>
    public static IHostBuilder UseWindowsServiceEx(this IHostBuilder hostBuilder) {
        return UseWindowsServiceEx(hostBuilder, _ => { });
    }

    /// <summary>
    /// Sets the host lifetime to <see cref="WindowsServiceLifetimeEx"/> and enables logging to the event log with the application
    /// name as the default source name.
    /// </summary>
    /// <remarks>
    /// This is context aware and will only activate if it detects the process is running
    /// as a Windows Service.
    /// </remarks>
    /// <param name="hostBuilder">The <see cref="IHostBuilder"/> to operate on.</param>
    /// <param name="configure">An <see cref="Action{WindowsServiceLifetimeOptions}"/> to configure the provided <see cref="WindowsServiceLifetimeOptions"/>.</param>
    /// <returns>The <paramref name="hostBuilder"/> instance for chaining.</returns>
    public static IHostBuilder UseWindowsServiceEx(this IHostBuilder hostBuilder, Action<WindowsServiceLifetimeOptions> configure) {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        if (WindowsServiceHelpers.IsWindowsService()) {
            hostBuilder.ConfigureServices(services => {
                AddWindowsServiceLifetimeEx(services, configure);
            });
        }

        return hostBuilder;
    }

    /// <summary>
    /// Configures the lifetime of the <see cref="IHost"/> built from <paramref name="services"/> to
    /// <see cref="WindowsServiceLifetimeEx"/> and enables logging to the event log with the application
    /// name as the default source name.
    /// </summary>
    /// <remarks>
    /// This is context aware and will only activate if it detects the process is running
    /// as a Windows Service.
    /// </remarks>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> used to build the <see cref="IHost"/>.
    /// For example, <see cref="HostApplicationBuilder.Services"/> or the <see cref="IServiceCollection"/> passed to the
    /// <see cref="IHostBuilder.ConfigureServices(Action{HostBuilderContext, IServiceCollection})"/> callback.
    /// </param>
    /// <returns>The <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddWindowsServiceEx(this IServiceCollection services) {
        return AddWindowsServiceEx(services, _ => { });
    }

    /// <summary>
    /// Configures the lifetime of the <see cref="IHost"/> built from <paramref name="services"/> to
    /// <see cref="WindowsServiceLifetimeEx"/> and enables logging to the event log with the application name as the default source name.
    /// </summary>
    /// <remarks>
    /// This is context aware and will only activate if it detects the process is running
    /// as a Windows Service.
    /// </remarks>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> used to build the <see cref="IHost"/>.
    /// For example, <see cref="HostApplicationBuilder.Services"/> or the <see cref="IServiceCollection"/> passed to the
    /// <see cref="IHostBuilder.ConfigureServices(Action{HostBuilderContext, IServiceCollection})"/> callback.
    /// </param>
    /// <param name="configure">An <see cref="Action{WindowsServiceLifetimeOptions}"/> to configure the provided <see cref="WindowsServiceLifetimeOptions"/>.</param>
    /// <returns>The <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddWindowsServiceEx(this IServiceCollection services, Action<WindowsServiceLifetimeOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);

        if (WindowsServiceHelpers.IsWindowsService()) {
            AddWindowsServiceLifetimeEx(services, configure);
        }

        return services;
    }

    private static void AddWindowsServiceLifetimeEx(IServiceCollection services, Action<WindowsServiceLifetimeOptions> configure) {
#if !NETFRAMEWORK
        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
#endif

        services.AddLogging(logging => {
#if !NETFRAMEWORK
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
#endif
            logging.AddEventLog();
        });
        services.AddSingleton<IHostLifetime, WindowsServiceLifetimeEx>();
        services.AddSingleton<IConfigureOptions<EventLogSettings>, EventLogSettingsSetup>();
        services.Configure(configure);
    }

    private sealed class EventLogSettingsSetup : IConfigureOptions<EventLogSettings> {
        private readonly string? _applicationName;

        public EventLogSettingsSetup(IHostEnvironment environment) {
            _applicationName = environment.ApplicationName;
        }

        public void Configure(EventLogSettings settings) {
#if !NETFRAMEWORK
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
#endif

            if (string.IsNullOrEmpty(settings.SourceName)) {
                settings.SourceName = _applicationName;
            }
        }
    }
}
