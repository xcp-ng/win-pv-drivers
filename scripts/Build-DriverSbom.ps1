[CmdletBinding()]
param (
    [Parameter()]
    [string[]]$Drivers = @("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd"),
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\..\branding.ps1
. $PSScriptRoot\..\scripts\branding-generic.ps1
. $PSScriptRoot\..\scripts\sign.ps1

$OutputPath = "$PSScriptRoot\..\installer\output"
foreach ($repo in $Drivers) {
    Push-Location $PSScriptRoot\..\$repo
    try {
        $DriverOutput = "$OutputPath\$Platform\$Configuration\$repo"

        sbom.exe generate -b $DriverOutput -bc . -D true -ps $Env:VENDOR_NAME -pn $repo -pv (Get-PackageVersion $repo)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool failed with error $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}
