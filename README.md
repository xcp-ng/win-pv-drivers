# Windows PV Drivers for XCP-ng

This repo serves as the release location for the Windows PV guest drivers for
XCP-ng guests.

The relevant source code may be found in these locations:

* [Installer](https://github.com/xcp-ng/win-installer)
* Drivers:
    * [Xen Bus](https://github.com/xcp-ng/win-xenbus)
    * [Xen Guest Agent](https://github.com/xcp-ng/win-xenguestagent)
    * [Xen Interface](https://github.com/xcp-ng/win-xeniface)
    * [Xen Paravirtual Network Class](https://github.com/xcp-ng/win-xenvif)
    * [Xen Paravirtual Network Device Driver](https://github.com/xcp-ng/win-xennet)
    * [Xen Paravirtual Storage Class Driver](https://github.com/xcp-ng/win-xenvbd)
    * [The XS Windows Volume Shadow Copy Service Provider](https://github.com/xcp-ng/win-xenvss)
* [Management Agent](https://github.com/xcp-ng/win-xenguestagent)

# Requirements

* Visual Studio 2012 (tested with VS 2012 Ultimate)
* Windows SDK for Windows 8
* WDK 8
* WDK 8 Redistributable Components
* WIX (tested with v3.6)
	* Starting with WIX v4, wix is available as a .NET tool that can be obtained as following
		* dotnet tool install --global wix
* EWDK for Windows 10, version 1903 with Visual Studio Build Tools 16.0
* git

WDK, SDK, and EWDK dependencies can be found [here](https://docs.microsoft.com/en-us/windows-hardware/drivers/other-wdk-downloads).

# Required Environment Variables

* `BUILD_ENV`:
    * Must be set to the path of the directory containing EWDK's SetupBuildEnv.cmd. It may be found in the EWDK mount and is called BuildEnv/ (e.g., `E:\BuildEnv\SetupBuildEnv.cmd`).
* `VS`:
    * The path to VS 2012 (e.g., `C:\Program Files (x86)\Microsoft Visual Studio 11.0\`)
* `KIT`:
    * The path to the (e.g., `C:\Program Files (x86)\Windows Kits\8.0\`)
* `WIX`:
    * The path to the Wix install (e.g., `C:\Program Files (x86)\WiX Toolset v3.6\`)

# Usage

First, download all projects:

```powershell
$ python .\build.py fetch
```

Second, after mounting the EWDK, set the environment variables and then build all projects:

```powershell
$ $ENV:BUILD_ENV = "E:\BuildEnv\SetupBuildEnv.cmd"
$ $ENV:VS = "C:\Program Files (x86)\Microsoft Visual Studio 11.0\"
$ $ENV:KIT = "C:\Program Files (x86)\Windows Kits\8.0\"
$ $ENV:WIX = "C:\Program Files (x86)\WiX Toolset v3.6\"
$ python .\build.py build  # Build the installers and output the certs used for signing/testing
```

# Build / Sign Process

First, the necessary repositories are pulled upon calling `python .\build.py fetch`

Second, `python .\build.py build` initiates a signed build.

The build and sign process consists of the following steps:

1. Iterate through each repository (except for the installer) and invoke `build.py` or `build.ps1` inside the
   repository directory. For all projects except for win-xenvss, the build script is ran inside the EWDK build environment. Otherwise, it is built in the native shell environment.
2. Create the directory tree of build outputs required by the installer. It is located in a temporary
   directory.
3. Generate a test signing certificate using `MakeCert`.
4. Build the installer using `python .\build.py --local $outputs_path --sign $cert_path`. The installer's
   `build.py` script signs all of the binaries.
5. Copy managementagentx64.msi, managementagentx86.msi, Setup.exe, and the test certificate (e.g., XCP-ng.cer)
   to the `output` directory.
6. Build a zip file from the files in step 5 and place it in `output`.
7. The resulting `output` directory looks like the following:
    ```
    output\managementagentx64.msi
    output\managementagentx86.msi
    output\Setup.exe
    output\win-pv-drivers.zip
    output\XCP-ng.cer
    ```
