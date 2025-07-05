# Driver offboarding

See the CA sequencing in [Driver.wxi](/installer/Driver.wxi).

## Xenbus and Xenvbd

See [CleanupActions.cs](/DriverInstallCustomAction/CleanupActions.cs).

## Xenvif

Each network adapter on Windows has a separate interface configuration bound to the interface GUID, which is itself bound to the device ID.
While Xenvif contains functionalities to save emulated interface settings and apply them to the PV interface, there exists no functionality in the other direction.
Thus the offboarding helper script [Copy-XenVifSettings.ps1](/XenDriverUtils/Copy-XenVifSettings.ps1).

It consists of two components:

- A preinstall task that backs up PV interface config;
- A postreboot task that reapplies the backed up configs to the emulated interfaces.
