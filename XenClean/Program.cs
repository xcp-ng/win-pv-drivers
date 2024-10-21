using XenClean;
using XenDriverUtils;

Logger.SetLogger(new ConsoleLogger());
UninstallProducts.Execute();
UninstallDevices.Execute();
UninstallDrivers.Execute();
UninstallRegistry.Execute();
Logger.Log("Finished, you must restart!");
