[CmdletBinding()]
param (
    [Parameter()]
    [string]$Target = "Rebuild",
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [switch]$Sbom
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

Push-Location $PSScriptRoot\xentimeprovider
try {
    nuget.exe restore xentimeprovider.sln
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet restore failed with error $LASTEXITCODE"
    }

    $BuildArgs = @(
        (Resolve-Path xentimeprovider.sln),
        "/m:4",
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        "/t:$Target"
    )

    msbuild.exe @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with error $LASTEXITCODE"
    }

    if ($Sbom) {
        sbom.exe generate -b .\$Platform\$Configuration -bc . -D true -ps $Env:VENDOR_NAME -pn XenTimeProvider -pv (Get-PackageVersion XenTimeProvider)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool failed with error $LASTEXITCODE"
        }
    }
}
finally {
    Pop-Location
}
