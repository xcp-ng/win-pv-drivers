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
. $PSScriptRoot/branding-generic.ps1

$OutputPath = "$PSScriptRoot/installer/output"
Remove-Item -Path $OutputPath -Force -Recurse -ErrorAction SilentlyContinue
$ErrorActionPreference = "Stop"
foreach ($repo in @("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd")) {
    Push-Location $PSScriptRoot/$repo
    try {
        $Env:MAJOR_VERSION = (Get-PackageVersion $repo).Major
        $Env:MINOR_VERSION = (Get-PackageVersion $repo).Minor
        $Env:MICRO_VERSION = (Get-PackageVersion $repo).Build
        $Env:BUILD_NUMBER = (Get-PackageVersion $repo).Revision
        ./build.ps1 `
            -Type $Type `
            -Arch $Arch `
            -SignMode $SignMode `
            -TestCertificate $Env:SIGNER_THUMBPRINT `
            -CodeQL:$CodeQL `
            -Sdv:$Sdv
        New-Item -ItemType Directory -Path $OutputPath/$repo -Force
        Copy-Item -Path ./$repo/* -Destination $OutputPath/$repo -Recurse -Force
    }
    finally {
        $Env:MAJOR_VERSION = ''
        $Env:MINOR_VERSION = ''
        $Env:MICRO_VERSION = ''
        $Env:BUILD_NUMBER = ''
        Pop-Location
    }
}
