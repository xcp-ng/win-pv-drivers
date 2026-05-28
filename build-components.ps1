[CmdletBinding()]
param (
    [Parameter()]
    [string]$Target = "Rebuild",
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

msbuild.exe `
    "$PSScriptRoot\installer\installer.slnx" `
    /t:DriverInstallCustomAction:$Target `
    /t:XenClean:$Target `
    /t:XenBootFix:$Target `
    /restore `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with error $LASTEXITCODE"
}

dotnet.exe publish .\xenplus\xenplus.csproj -c $Configuration -r win-$Platform -o .\xenplus\bin\publish\x64\Release
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with error $LASTEXITCODE"
}
