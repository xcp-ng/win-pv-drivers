using XenClean;
using XenDriverUtils;

Logger.SetLogger(new ConsoleLogger());
UninstallProducts.Execute();
UninstallDevices.Execute();
UninstallDrivers.Execute();
XenCleanup.XenbusCleanup();
XenCleanup.ResetNvmeOverride();
XenCleanup.XenfiltReset();
Logger.Log("Finished, you must restart!");
