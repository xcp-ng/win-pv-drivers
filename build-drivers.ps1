[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("free", "checked")]
    [string]$Type,
    [Parameter()]
    [string]$Arch,
    [Parameter()]
    [ValidateSet("TestSign", "ProductionSign", "Off")]
    [string]$SignMode = "TestSign",
    [Parameter()]
    [switch]$CodeQL,
    [Parameter()]
    [switch]$Sdv
)

. $PSScriptRoot/branding.ps1

$OutputPath = "$PSScriptRoot/installer/output"
Remove-Item -Path $OutputPath -Force -Recurse -ErrorAction SilentlyContinue
$ErrorActionPreference = "Stop"
foreach ($repo in @("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd")) {
    Push-Location $PSScriptRoot/$repo
    try {
        ./build.ps1 `
            -Type $Type `
            -Arch $Arch `
            -SignMode $SignMode `
            -TestCertificate $Env:SigningCertificateThumbprint `
            -CodeQL:$CodeQL `
            -Sdv:$Sdv
        New-Item -ItemType Directory -Path $OutputPath/$repo -Force
        Copy-Item -Path ./$repo/* -Destination $OutputPath/$repo -Recurse -Force
    }
    finally {
        Pop-Location
    }
}
