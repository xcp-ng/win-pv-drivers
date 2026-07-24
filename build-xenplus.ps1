[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x64")]
    [string]$Platform
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1

$OutputDir = "$PSScriptRoot\xenplus\bin\publish\$Platform\$Configuration"

dotnet.exe publish `
    "$PSScriptRoot\xenplus\xenplus.csproj" `
    --configuration $Configuration `
    --runtime "win-$Platform" `
    --output $OutputDir `
    -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish xenplus failed with error $LASTEXITCODE"
}

dotnet.exe publish `
    "$PSScriptRoot\xenplus_session\xenplus_session.csproj" `
    --configuration $Configuration `
    --runtime "win-$Platform" `
    --output $OutputDir `
    -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish xenplus_session failed with error $LASTEXITCODE"
}
