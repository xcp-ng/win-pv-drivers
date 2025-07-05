using XenClean;
using XenDriverUtils;

Logger.SetLogger(new ConsoleLogger());
XenOffboard.BackupXenvif();
XenOffboard.PrepareRestoreXenvif();
UninstallProducts.Execute();
UninstallDevices.Execute();
UninstallDrivers.Execute();
UninstallServices.Execute();
UninstallRegistry.Execute();
Logger.Log("Finished, you must restart!");
