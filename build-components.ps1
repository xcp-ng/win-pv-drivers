[CmdletBinding()]
param (
    [Parameter()]
    [string]$Target = "Rebuild",
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
. $PSScriptRoot\scripts\sign.ps1

msbuild.exe `
    "$PSScriptRoot\installer\installer.slnx" `
    /t:DriverInstallCustomAction:$Target `
    /t:XenBootFix:$Target `
    /restore `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with error $LASTEXITCODE"
}

msbuild.exe `
    "$PSScriptRoot\installer\installer.slnx" `
    /t:XenClean:$Target `
    /restore `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:RuntimeIdentifier=win-$Platform `
    /p:SelfContained=true
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with error $LASTEXITCODE"
}

$ComponentsDir = "$PSScriptRoot\components\$Platform\$Configuration"
Remove-Item $ComponentsDir -Recurse -Force -ErrorAction SilentlyContinue
if ($Target -ine "Clean") {
    $ComponentOutputs = @{
        DriverInstallCustomAction = "$PSScriptRoot\DriverInstallCustomAction\bin\$Platform\$Configuration\net462"
        XenDriverUtils            = "$PSScriptRoot\XenDriverUtils\bin\$Platform\$Configuration\net462"
        XenClean                  = "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\win-$Platform\publish"
        XenBootFix                = "$PSScriptRoot\XenBootFix\$Platform\$Configuration"
    }
    foreach ($Component in $ComponentOutputs.GetEnumerator()) {
        $Destination = Join-Path $ComponentsDir $Component.Key
        New-Item -Path $Destination -ItemType Directory -Force | Out-Null
        Copy-Item -Path "$($Component.Value)\*" -Destination $Destination -Recurse -Force
    }
}
