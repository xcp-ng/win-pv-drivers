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
    [string]$TimeProvider = "$PSScriptRoot\..\input\timeprovider.zip",
    [Parameter()]
    [string]$Xstdvga = "$PSScriptRoot\..\input\xstdvga.zip"
)

$ErrorActionPreference = "Stop"

# specifically use the Windows bsdtar
$tar = Join-Path ([System.Environment]::SystemDirectory) "tar.exe"

$DriversDir = "$PSScriptRoot\..\installer\driver-bins"
Remove-Item $DriversDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $DriversDir -ItemType Directory -Force | Out-Null
& $tar -xvf $Drivers -C $DriversDir
if ($LASTEXITCODE -ne 0) {
    throw "extracting Drivers failed with error $LASTEXITCODE"
}

if ($DriversSigned) {
    & $tar -xvf $DriversSigned -C "$DriversDir\$Platform\$Configuration" --strip-components 1
    if ($LASTEXITCODE -ne 0) {
        throw "extracting DriversSigned failed with error $LASTEXITCODE"
    }
}

$TimeProviderDir = "$PSScriptRoot\..\xentimeprovider\$Platform\$Configuration"
Remove-Item $TimeProviderDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $TimeProviderDir -ItemType Directory -Force | Out-Null
& $tar -xvf $TimeProvider -C $TimeProviderDir
if ($LASTEXITCODE -ne 0) {
    throw "extracting TimeProvider failed with error $LASTEXITCODE"
}

$XstdvgaDir = "$PSScriptRoot\..\xstdvga\vs2022\$Platform\$Configuration\xstdvga"
Remove-Item $XstdvgaDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $XstdvgaDir -ItemType Directory -Force | Out-Null
& $tar -xvf $Xstdvga -C $XstdvgaDir --strip-components 1
if ($LASTEXITCODE -ne 0) {
    throw "extracting Xstdvga failed with error $LASTEXITCODE"
}
