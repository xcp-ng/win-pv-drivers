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
    * MSVC v143 - VS 2022 C++ x64/x86 Spectre-mitigated libs (Latest)
* Windows SDK for Windows 10/11 (tested with 10.0.22621 and 10.0.26100)
* Windows Driver Kit matching your Windows SDK
* git

Windows SDK and WDK dependencies can be found [here](https://docs.microsoft.com/en-us/windows-hardware/drivers/other-wdk-downloads).

# Usage

Driver projects are provided as submodules of this repository:

```
git clone --recursive https://github.com/xcp-ng/win-pv-drivers.git
```

Using the x64 Native Tools Command Prompt for VS 2022, navigate to this repository and build the drivers:

```
powershell .\build-drivers.ps1 -Type free -Arch x64
```

Output drivers will be collected in `installer\output`.
Specify `$Env:SigningCertificateThumbprint` in `branding.ps1` to choose a specific signing certificate if necessary.
(A test certificate will be used for the drivers otherwise)

Next, build the installer and XenClean packages:

```
powershell .\build-installer.ps1 -Configuration Release -Platform x64 -Target Rebuild
```

Output files will be dropped in the `package` directory.
