# Coexistence with other Xen PV drivers and tools

Our driver package is not designed to coexist with other driver packages.
The following checks are made to ensure a clean and reliable installation/uninstallation:

- The installer's Upgrade table checks for other installed packages and denies installation if these are detected (see [Package.wxs](/installer/Package.wxs)). [XenClean](/XenClean/UninstallProducts.cs) removes any detected Xen PV driver packages.
- The [CheckIncompatibleDevices and Check3PStorageDrivers](/DriverInstallCustomAction/ImmediateActions.cs) custom actions prevent installation if existing Xen drivers, Xen vendor device or third-party storage drivers are present.
  If you're creating your own installer package, edit [XenDeviceInfo.cs](/XenDriverUtils/XenDeviceInfo.cs) to add your own customized device IDs if necessary.
- Uninstalling the Windows Guest Tools package will remove all existing Xen PV drivers and driver configuration.
