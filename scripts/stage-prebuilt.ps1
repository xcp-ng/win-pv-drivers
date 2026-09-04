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
    [switch]$SignDrivers,
    [Parameter()]
    [string]$Xenplus = "$PSScriptRoot\..\input\xenplus.zip",
    [Parameter()]
    [switch]$SignXenplus,
    [Parameter()]
    [string]$Xstdvga = "$PSScriptRoot\..\input\xstdvga.zip",
    [Parameter()]
    [switch]$SignXstdvga,
    [Parameter()]
    [string]$XstdvgaSigned,
    [Parameter()]
    [string]$Components = "$PSScriptRoot\..\input\components.zip",
    [Parameter()]
    [switch]$SignComponents,
    [Parameter()]
    [string]$StagingRoot = "$PSScriptRoot\..\staging"
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\..\branding.ps1
. $PSScriptRoot\sign.ps1

# specifically use the Windows bsdtar
$tar = Join-Path ([System.Environment]::SystemDirectory) "tar.exe"

function Expand-Artifact {
    param (
        [Parameter(Mandatory)]
        [string]$Archive,
        [Parameter(Mandatory)]
        [string]$Destination,
        [Parameter()]
        [switch]$Replace,
        [Parameter()]
        [int]$StripComponents = 0
    )

    if (!(Test-Path -LiteralPath $Archive -PathType Leaf)) {
        throw "Artifact '$Archive' doesn't exist"
    }
    if ($Replace) {
        Remove-Item -LiteralPath $Destination -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -Path $Destination -ItemType Directory -Force | Out-Null

    $TarArgs = @("-xvf", $Archive, "-C", $Destination)
    if ($StripComponents) {
        $TarArgs += @("--strip-components", $StripComponents)
    }
    & $tar @TarArgs
    if ($LASTEXITCODE -ne 0) {
        throw "extracting '$Archive' failed with error $LASTEXITCODE"
    }
}

$ConfigurationDir = Join-Path $StagingRoot "$Platform\$Configuration"
$DriversDir = Join-Path $ConfigurationDir "drivers"
$XenplusDir = Join-Path $ConfigurationDir "xenplus"
$XstdvgaDir = Join-Path $ConfigurationDir "xstdvga"
$ComponentsDir = Join-Path $ConfigurationDir "components"

if ($Drivers) {
    Expand-Artifact -Archive $Drivers -Destination $DriversDir -Replace
    if ($SignDrivers) {
        Set-SignerFileSignature (Get-ChildItem $DriversDir -File -Recurse -Include *.sys, *.dll, *.exe, *.cat)
    }
}
if ($DriversSigned) {
    Expand-Artifact -Archive $DriversSigned -Destination $DriversDir -StripComponents 1
}

if ($Xenplus) {
    Expand-Artifact -Archive $Xenplus -Destination $XenplusDir -Replace
    if ($SignXenplus) {
        Set-SignerFileSignature (Get-ChildItem $XenplusDir -File -Recurse -Include *.exe)
    }
}

if ($Xstdvga) {
    Expand-Artifact -Archive $Xstdvga -Destination $XstdvgaDir -Replace
    if ($SignXstdvga) {
        Set-SignerFileSignature (Get-ChildItem $XstdvgaDir -File -Recurse -Include *.sys, *.cat)
    }
}
if ($XstdvgaSigned) {
    Expand-Artifact -Archive $XstdvgaSigned -Destination $XstdvgaDir -StripComponents 1
}

if ($Components) {
    Expand-Artifact -Archive $Components -Destination $ComponentsDir -Replace
    if ($SignComponents) {
        Set-SignerFileSignature (Get-ChildItem $ComponentsDir -File -Recurse -Include xeninst.CA.dll, xdutils.dll, XenClean.exe, XenBootFix.exe)
    }
}
