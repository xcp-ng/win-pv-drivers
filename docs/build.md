# Build guide

## Requirements

* Visual Studio 2022 with the following features:
    * Desktop development with C++ workload
    * .NET desktop development workload
    * .NET Framework 4.6.2 targeting pack
    * MSVC v143 - VS 2022 C++ x64/x86 Spectre-mitigated libs (Latest)
    * Windows Driver Kit extensions for Visual Studio (listed simply as "Windows Driver Kit" in the Visual Studio Installer)
* Windows SDK for Windows 10/11 (tested with 10.0.22621 and 10.0.26100)
* Windows Driver Kit matching your Windows SDK
* Git for Windows
* Rustup and latest Rust stable

Windows SDK and WDK dependencies can be found on [Microsoft Learn](https://learn.microsoft.com/en-us/windows-hardware/drivers/other-wdk-downloads).

## Branding and configuration

You customize the driver and installer package by creating `branding.ps1` inside the repository root.

See below for an example of the `branding.ps1` file.

```powershell
$Env:VENDOR_NAME = 'Xen Project'
$Env:PRODUCT_NAME = 'Xen'
$Env:VENDOR_PREFIX = 'XP'
$Env:COPYRIGHT = 'Copyright (c) Xen Project.'

# These version numbers are used in multiple places.
# You must have at least 2 version components (major.minor).
# The third and fourth components will be 0-filled if absent, except in drivers where the revision will be the build time prefixed with "1".
# For compatibility reasons, it's not recommended to have any version component exceed 255.255.65535.65535.
$PackageVersions = @{
    Product       = '9.0.9000.0'
    xenbus        = '9.0.9001.0'  # defaults to product version
    xencons       = '9.0.9002.0'  # defaults to product version
    xenhid        = '9.0.9003.0'  # defaults to product version
    xeniface      = '9.0.9004.0'  # defaults to product version
    xennet        = '9.0.9005.0'  # defaults to product version
    xenvbd        = '9.0.9006.0'  # defaults to product version
    xenvif        = '9.0.9007.0'  # defaults to product version
    xenvkbd       = '9.0.9008.0'  # defaults to product version
    XenClean      = '9.0.9009.0'  # defaults to product version
    XenBootFix    = '9.0.9010.0'  # defaults to product version
    XenGuestAgent = '9.0.9011.0'  # defaults to product version
}

# These variables influence the UpgradeCode property of generated MSI packages.
# To avoid conflict between installers of different vendors, you must change
# these values to a random GUID if building your own installer.
$Env:MSI_UPGRADE_CODE_X86 = '{GUIDHERE-GUID-HERE-GUID-HEREGUIDHERE}'
$Env:MSI_UPGRADE_CODE_X64 = '{GUIDHERE-GUID-HERE-GUID-HEREGUIDHERE}'

$Env:SIGNER = "<signer certificate thumbprint or PFX path>"
```

Specify `$Env:SIGNER` in `branding.ps1` to choose a specific signing certificate thumbprint or PFX path if necessary.
(A test certificate will be used for the drivers otherwise)

## Building drivers

Using the x64 Native Tools Command Prompt for VS 2022, navigate to this repository and build the drivers:

```powershell
.\build-drivers.ps1 -Configuration Release -Platform x64
```

Output drivers will be collected in `installer\output`.

If you need to sign the drivers externally (e.g. WHQL signatures), you must replace the drivers found here with your own signed binaries.
These binaries should be located at `installer\output\<driver name>\<platform>\<configuration>` for each driver included in the package.

## Building the Rust-based Windows guest agent

Simply run the command:

```powershell
.\build-guestagent.ps1 -Configuration release
```

The binaries are located at `xen-guest-agent\target\<configuration>`.

## Building the installer and release package

Next, build the installer and XenClean packages:

```powershell
.\build-installer.ps1 -Configuration Release -Platform x64 -Target Rebuild
```

By default, output files will be dropped in the `output\<version>-<configuration>-<platform>` directory.
You may specify `-ExportSymbols` or `-ExportCertificate` to include the debug symbols and signer certificate (along with testsigning scripts) in your output package respectively.
