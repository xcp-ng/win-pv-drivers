[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [string]$Drivers = "$PSScriptRoot\input\drivers.zip",
    [Parameter()]
    [string]$GuestAgent = "$PSScriptRoot\input\guestagent.zip",
    [Parameter()]
    [string]$TimeProvider = "$PSScriptRoot\input\timeprovider.zip",
    [Parameter()]
    [string]$Xstdvga = "$PSScriptRoot\input\xstdvga.zip"
)

$ErrorActionPreference = "Stop"

$DriversDir = "$PSScriptRoot\installer\driver-bins"
Remove-Item $DriversDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $DriversDir -ItemType Directory -Force
7z.exe x $Drivers "-o$DriversDir"
if ($LASTEXITCODE -ne 0) {
    throw "7z for Drivers failed with error $LASTEXITCODE"
}

$GuestAgentDir = "$PSScriptRoot\xen-guest-agent\target\$Configuration"
Remove-Item $GuestAgentDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $GuestAgentDir -ItemType Directory -Force
7z.exe x $GuestAgent "-o$GuestAgentDir"
if ($LASTEXITCODE -ne 0) {
    throw "7z for GuestAgent failed with error $LASTEXITCODE"
}

$TimeProviderDir = "$PSScriptRoot\xentimeprovider\$Platform\$Configuration"
Remove-Item $TimeProviderDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $TimeProviderDir -ItemType Directory -Force
7z.exe x $TimeProvider "-o$TimeProviderDir"
if ($LASTEXITCODE -ne 0) {
    throw "7z for TimeProvider failed with error $LASTEXITCODE"
}

$XstdvgaDir = "$PSScriptRoot\xstdvga\vs2022\$Platform\$Configuration\xstdvga"
Remove-Item $XstdvgaDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $XstdvgaDir -ItemType Directory -Force
# note the different extraction mode for xstdvga
7z.exe e $Xstdvga "-o$XstdvgaDir"
if ($LASTEXITCODE -ne 0) {
    throw "7z for Xstdvga failed with error $LASTEXITCODE"
}
