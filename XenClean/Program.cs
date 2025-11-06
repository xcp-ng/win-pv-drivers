using XenClean;
using XenDriverUtils;

Logger.SetLogger(new ConsoleLogger());
if (XenCleanup.IsSafeMode()) {
    Logger.Log("Skipping Xenvif offboarding in Safe Mode");
} else {
    XenOffboard.BackupXenvif();
    XenOffboard.PrepareRestoreXenvif();
}
if (XenCleanup.IsSafeMode()) {
    Logger.Log("Skipping product uninstallation in Safe Mode");
} else {
    UninstallProducts.Execute();
}
UninstallDevices.Execute();
UninstallDrivers.Execute();
UninstallServices.Execute(!XenCleanup.IsSafeMode());
UninstallRegistry.Execute();
Logger.Log("Finished, you must restart!");
