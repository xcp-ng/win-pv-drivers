using System.ComponentModel;
using System.ServiceProcess;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace XenPlus;

sealed class WindowsServicePowerEventArgs(PowerBroadcastStatus powerStatus) : CancelEventArgs(false) {
    public PowerBroadcastStatus PowerBroadcastStatus => powerStatus;
}

sealed class WindowsServiceSessionChangeEventArgs(SessionChangeDescription changeDescription) : EventArgs {
    public SessionChangeDescription SessionChangeDescription => changeDescription;
}

sealed class WindowsServiceLifetimeEx(
    IHostEnvironment environment,
    IHostApplicationLifetime applicationLifetime,
    ILoggerFactory loggerFactory,
    IOptions<HostOptions> optionsAccessor,
    IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor)
    : WindowsServiceLifetime(
        environment,
        applicationLifetime,
        loggerFactory,
        optionsAccessor,
        windowsServiceOptionsAccessor) {
    public delegate void WindowsServicePowerEventHandler(object? sender, WindowsServicePowerEventArgs args);

    public event WindowsServicePowerEventHandler? PowerBroadcast;

    public delegate void WindowsServiceSessionChangeEventHandler(object? sender, WindowsServiceSessionChangeEventArgs args);

    public event WindowsServiceSessionChangeEventHandler? SessionChange;

    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus) {
        var args = new WindowsServicePowerEventArgs(powerStatus);
        PowerBroadcast?.Invoke(this, args);
        return !args.Cancel;
    }

    protected override void OnSessionChange(SessionChangeDescription changeDescription) {
        var args = new WindowsServiceSessionChangeEventArgs(changeDescription);
        SessionChange?.Invoke(this, args);
    }
}
