# Windows PV Drivers for XCP-ng

This repo contains the Windows PV guest driver installer for XCP-ng guests.

The relevant source code may be found in these locations:

* Drivers:
    * [Bus Device Driver](https://github.com/xcp-ng/win-xenbus)
    * [Interface Driver](https://github.com/xcp-ng/win-xeniface)
    * [Network Class Driver](https://github.com/xcp-ng/win-xenvif)
    * [Network Device Driver](https://github.com/xcp-ng/win-xennet)
    * [Storage Class Driver](https://github.com/xcp-ng/win-xenvbd)
    * [Console Driver](https://github.com/xcp-ng/win-xencons)
    * [Keyboard/Mouse Driver](https://github.com/xcp-ng/win-xenvkbd)
    * [HID Minidriver](https://github.com/xcp-ng/win-xenhid)

# Requirements

* Visual Studio 2022 with the following features:
    * Desktop development with C++ workload
    * .NET desktop development workload
    * .NET Framework 4.6.2 targeting pack
    * MSVC v143 - VS 2022 C++ x64/x86 Spectre-mitigated libs (Latest)
    * Windows Driver Kit extensions for Visual Studio (listed simply as "Windows Driver Kit" in the Visual Studio Installer)
* Windows SDK for Windows 10/11 (tested with 10.0.22621 and 10.0.26100)
* Windows Driver Kit matching your Windows SDK
* Git for Windows
* PowerShell 7.3+
* Rustup and latest Rust stable

Windows SDK and WDK dependencies can be found [here](https://docs.microsoft.com/en-us/windows-hardware/drivers/other-wdk-downloads).

# Usage

Driver projects are provided as submodules of this repository:

```
git clone --recursive https://github.com/xcp-ng/win-pv-drivers.git
```

## Branding and configuration

You customize the driver and installer package by creating `branding.ps1` inside this directory.

See below for an example of the `branding.ps1` file.

```powershell
$Env:VENDOR_NAME = 'Xen Project'
$Env:PRODUCT_NAME = 'Xen'
$Env:VENDOR_PREFIX = 'XP'
$Env:COPYRIGHT = 'Copyright (c) Xen Project.'

$PackageVersions = @{
    Product    = '9.0.9000.0'
    xenbus     = '9.0.9001.0'  # defaults to product version
    xencons    = '9.0.9002.0'  # defaults to product version
    xenhid     = '9.0.9003.0'  # defaults to product version
    xeniface   = '9.0.9004.0'  # defaults to product version
    xennet     = '9.0.9005.0'  # defaults to product version
    xenvbd     = '9.0.9006.0'  # defaults to product version
    xenvif     = '9.0.9007.0'  # defaults to product version
    xenvkbd    = '9.0.9008.0'  # defaults to product version
    XenClean   = '9.0.9009.0'  # defaults to product version
    XenBootFix = '9.0.9010.0'  # defaults to product version
}

# These variables influence the UpgradeCode property of generated MSI packages.
# To avoid conflict between installers of different vendors, you must change
# these values to a random GUID if building your own installer.
$Env:MSI_UPGRADE_CODE_X86 = '{10828840-D8A9-4953-B44A-1F1D3CD7ECB0}'
$Env:MSI_UPGRADE_CODE_X64 = '{D60FED1E-316C-41B0-B7A5-E44951A82618}'

$Env:SIGNER = "<signer certificate thumbprint or PFX path>"
```

Specify `$Env:SIGNER` in `branding.ps1` to choose a specific signing certificate thumbprint or PFX path if necessary.
(A test certificate will be used for the drivers otherwise)

## Building drivers

Using the x64 Native Tools Command Prompt for VS 2022, navigate to this repository and build the drivers:

```
powershell .\build-drivers.ps1 -Configuration Release -Platform x64
```

Output drivers will be collected in `installer\output`.

If you need to sign the drivers externally (e.g. WHQL signatures), you must replace the drivers found here with your own signed binaries.
These binaries should be located at `installer\output\<driver name>\<platform>\<configuration>` for each driver included in the package.

## Building the Rust-based Windows guest agent

Simply run the command:

```
powershell .\build-guestagent.ps1 -Configuration release
```

The binaries are located at `xen-guest-agent\target\<configuration>`.

## Building the installer and release package

Next, build the installer and XenClean packages:

```
powershell .\build-installer.ps1 -Configuration Release -Platform x64 -Target Rebuild
```

By default, output files will be dropped in the `output\<version>-<configuration>-<platform>` directory.
You may specify `-ExportSymbols` or `-ExportCertificate` to include the debug symbols and signer certificate (along with testsigning scripts) in your output package respectively.

## Coexistence with other Xen PV drivers

Our driver package is not designed to coexist with other driver packages.
The following checks are made to ensure a clean and reliable installation/uninstallation:

- The Upgrade table checks for other installed packages and denies installation if these are detected (see [Package.wxs](installer/Package.wxs)). [XenClean](XenClean/UninstallProducts.cs) removes any detected Xen PV driver packages.
- The [CheckIncompatibleDevices and Check3PStorageDrivers](DriverInstallCustomAction/ImmediateActions.cs) custom actions prevent installation if existing Xen drivers, Xen vendor device or third-party storage drivers are present.
If you're creating your own installer package, edit [XenDeviceInfo.cs](XenDriverUtils/XenDeviceInfo.cs) to add your own customized device IDs if necessary.
- Uninstalling the Windows PV Drivers package will remove all existing Xen PV drivers and driver configuration.
