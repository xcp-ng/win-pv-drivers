# Build guide

## Prerequisites

* Visual Studio 2026 with the following features:
    * Desktop development with C++ workload
    * .NET desktop development workload
    * .NET Framework 4.6.2 targeting pack
    * .NET 10.0 Runtime
    * C++ Spectre-mitigated libraries for x64/x86 (Latest MSVC)
    * Windows Driver Kit extensions for Visual Studio (listed simply as "Windows Driver Kit" in the Visual Studio Installer)
* Windows SDK for Windows 11 (tested with 10.0.28000)
* Windows Driver Kit matching your Windows SDK
* Git for Windows
* [CodeQL CLI](https://github.com/github/codeql-cli-binaries) installed into PATH, with the necessary CodeQL packs
* [Microsoft SBOM Tool](https://github.com/microsoft/sbom-tool) available as `sbom.exe` in PATH (installable via WinGet)
* The correct version of [wix.zip](https://github.com/xcp-ng/wix-builder) extracted into `deps\wix` (see [build-installer.yml](/.github/workflows/build-installer.yml))

Windows SDK and WDK downloads can be found on [Microsoft Learn](https://learn.microsoft.com/en-us/windows-hardware/drivers/other-wdk-downloads).

## Branding and configuration

You customize the driver and installer package by creating `branding.ps1` inside the repository root.

See below for an example of the `branding.ps1` file.

```powershell
$Env:VENDOR_NAME = 'Xen Project'
$Env:PRODUCT_NAME = 'Xen'
$Env:VENDOR_PREFIX = 'XP'
$Env:COPYRIGHT = 'Copyright (c) Xen Project.'

# These are WinPV driver-specific settings recommended for use with the XCP-ng
# WinPV tools.
$Env:FORCE_ACTIVATE = '1'
$Env:FORCE_UNPLUG = '1'

# These version numbers are used in multiple places.
# You must have at least 2 version components (major.minor).
# The third and fourth components will be 0-filled if absent, except in drivers
# where the revision will be the build time prefixed with "1".
# For compatibility reasons, it's not recommended to have any version component
# exceed 255.255.65535.65535.
$PackageVersions = @{
    Product         = '9.0.9000.0'
    xenbus          = '9.0.9001.0'  # defaults to product version
    xencons         = '9.0.9002.0'  # defaults to product version
    xenhid          = '9.0.9003.0'  # defaults to product version
    xeniface        = '9.0.9004.0'  # defaults to product version
    xennet          = '9.0.9005.0'  # defaults to product version
    xenvbd          = '9.0.9006.0'  # defaults to product version
    xenvif          = '9.0.9007.0'  # defaults to product version
    xenvkbd         = '9.0.9008.0'  # defaults to product version
    XenClean        = '9.0.9009.0'  # defaults to product version
    XenBootFix      = '9.0.9010.0'  # defaults to product version
    xenplus         = '9.0.9011.0'  # defaults to product version
    XenTimeProvider = '9.0.9012.0'  # defaults to product version
    xstdvga         = '9.0.9013.0'  # defaults to product version
}

# These variables influence the UpgradeCode property of generated MSI packages.
# To avoid conflict between installers of different vendors, you must change
# these values to a random GUID if building your own installer.
$Env:MSI_UPGRADE_CODE_X86 = '{GUIDHERE-GUID-HERE-GUID-HEREGUIDHERE}'
$Env:MSI_UPGRADE_CODE_X64 = '{GUIDHERE-GUID-HERE-GUID-HEREGUIDHERE}'

$Env:SIGNER = "<signer certificate thumbprint or PFX path>"

$Env:PRODUCT_URL = "<put your branding URL here>"
```

Specify `$Env:SIGNER` in `branding.ps1` to choose a specific signing certificate thumbprint or PFX path if necessary.
(A test certificate will be used for the drivers otherwise)

## Building drivers

Using the Developer PowerShell for VS, navigate to this repository and build the drivers:

```powershell
.\build-drivers.ps1 -Configuration Release -Platform x64
```

Output drivers will be collected in `installer\driver-bins`.

If you need to sign the drivers externally (e.g. WHQL signatures), you must replace the drivers found here with your own signed binaries.
These binaries should be located at `installer\driver-bins\<platform>\<configuration>\<driver name>` for each driver included in the package.

## Building the Xen time provider

Run the command:

```powershell
.\build-timeprovider.ps1 -Configuration Release -Platform x64
```

The binaries are located at `xentimeprovider\<platform>\<configuration>`.

## Building the XSTDVGA driver

Run the command:

```powershell
.\build-xstdvga.ps1 -Configuration Release -Platform x64
```

The binaries are located at `xstdvga\<VS version>\<platform>\<configuration>\xstdvga`.

## Building the installer components

Run the command:

```powershell
.\build-components.ps1 -Configuration Release -Platform x64
```

## Building the installer and release package

Run the command:

```powershell
.\build-installer.ps1 -Configuration Release -Platform x64
```

By default, output files will be dropped in the `output\<version>-<configuration>-<platform>` directory.
You may specify `-ExportSymbols` or `-ExportCertificate` to include the debug symbols and signer certificate (along with testsigning scripts) in your output package respectively.
