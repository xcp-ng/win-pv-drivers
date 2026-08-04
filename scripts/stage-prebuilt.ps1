[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [string]$Drivers = "$PSScriptRoot\..\input\drivers.zip",
    [Parameter()]
    [string]$DriversSigned,
    [Parameter()]
    [switch]$ResignDrivers,
    [Parameter()]
    [string]$Xenplus = "$PSScriptRoot\..\input\xenplus.zip",
    [Parameter()]
    [string]$TimeProvider = "$PSScriptRoot\..\input\timeprovider.zip",
    [Parameter()]
    [string]$Xstdvga = "$PSScriptRoot\..\input\xstdvga.zip",
    [Parameter()]
    [switch]$ResignXstdvga,
    [Parameter()]
    [string]$XstdvgaSigned,
    [Parameter()]
    [string]$Components = "$PSScriptRoot\..\input\components.zip"
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\..\branding.ps1
. $PSScriptRoot\sign.ps1

# specifically use the Windows bsdtar
$tar = Join-Path ([System.Environment]::SystemDirectory) "tar.exe"

$DriversDir = "$PSScriptRoot\..\installer\driver-bins"

if ($Drivers) {
    Remove-Item $DriversDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path $DriversDir -ItemType Directory -Force | Out-Null
    & $tar -xvf $Drivers -C $DriversDir
    if ($LASTEXITCODE -ne 0) {
        throw "extracting Drivers failed with error $LASTEXITCODE"
    }

    if ($ResignDrivers) {
        Set-SignerFileSignature (Get-ChildItem "$DriversDir\$Platform\$Configuration" -File -Recurse -Include *.sys, *.dll, *.exe, *.cat)
    }
}

if ($DriversSigned) {
    & $tar -xvf $DriversSigned -C "$DriversDir\$Platform\$Configuration" --strip-components 1
    if ($LASTEXITCODE -ne 0) {
        throw "extracting DriversSigned failed with error $LASTEXITCODE"
    }
}

if ($Xenplus) {
    $XenplusDir = "$PSScriptRoot\..\xenplus\bin\publish\$Platform\$Configuration"
    Remove-Item $XenplusDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path $XenplusDir -ItemType Directory -Force | Out-Null
    & $tar -xvf $Xenplus -C $XenplusDir
    if ($LASTEXITCODE -ne 0) {
        throw "extracting Xenplus failed with error $LASTEXITCODE"
    }
}

if ($TimeProvider) {
    $TimeProviderDir = "$PSScriptRoot\..\xentimeprovider\$Platform\$Configuration"
    Remove-Item $TimeProviderDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path $TimeProviderDir -ItemType Directory -Force | Out-Null
    & $tar -xvf $TimeProvider -C $TimeProviderDir
    if ($LASTEXITCODE -ne 0) {
        throw "extracting TimeProvider failed with error $LASTEXITCODE"
    }
}

if ($Xstdvga) {
    $XstdvgaDir = "$PSScriptRoot\..\xstdvga\vs2022\$Platform\$Configuration\xstdvga"
    Remove-Item $XstdvgaDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path $XstdvgaDir -ItemType Directory -Force | Out-Null
    & $tar -xvf $Xstdvga -C $XstdvgaDir
    if ($LASTEXITCODE -ne 0) {
        throw "extracting Xstdvga failed with error $LASTEXITCODE"
    }

    if ($ResignXstdvga) {
        Set-SignerFileSignature (Get-ChildItem $XstdvgaDir -File -Recurse -Include *.sys, *.cat)
    }
}

if ($XstdvgaSigned) {
    $XstdvgaDir = "$PSScriptRoot\..\xstdvga\vs2022\$Platform\$Configuration\xstdvga"
    Remove-Item $XstdvgaDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path $XstdvgaDir -ItemType Directory -Force | Out-Null
    & $tar -xvf $XstdvgaSigned -C $XstdvgaDir --strip-components 1
    if ($LASTEXITCODE -ne 0) {
        throw "extracting XstdvgaSigned failed with error $LASTEXITCODE"
    }
}

if ($Components) {
    $ComponentsDir = "$PSScriptRoot\..\components\$Platform\$Configuration"
    Remove-Item $ComponentsDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path $ComponentsDir -ItemType Directory -Force | Out-Null
    & $tar -xvf $Components -C $ComponentsDir
    if ($LASTEXITCODE -ne 0) {
        throw "extracting Components failed with error $LASTEXITCODE"
    }
}
