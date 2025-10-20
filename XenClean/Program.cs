using System;
using XenClean;
using XenDriverUtils;

Logger.SetLogger(new ConsoleLogger());
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAFEBOOT_OPTION"))) {
    Logger.Log("Skipping Xenvif offboarding in Safe Mode");
} else {
    XenOffboard.BackupXenvif();
    XenOffboard.PrepareRestoreXenvif();
}
UninstallProducts.Execute();
UninstallDevices.Execute();
UninstallDrivers.Execute();
UninstallServices.Execute();
UninstallRegistry.Execute();
Logger.Log("Finished, you must restart!");
