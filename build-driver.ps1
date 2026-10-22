[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd")]
    [string]$Driver,
    [Parameter()]
    [string]$Target = "Build",
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [ValidateSet("vs2019", "vs2022")]
    [string]$SolutionDir = "vs2022",
    [Parameter()]
    [ValidateSet("Windows 10")]
    [string]$ConfigurationBase = "Windows 10",
    [Parameter()]
    [string]$BuildNumber,
    [Parameter()]
    [switch]$Kasan
)

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1

$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot\$Driver
try {
    $rawver = Get-PackageVersion -Raw $Driver
    $ver = Get-PackageVersion $Driver
    $Env:MAJOR_VERSION = $ver.Major
    $Env:MINOR_VERSION = $ver.Minor
    $Env:MICRO_VERSION = $ver.Build
    if ($rawver.Revision -ne -1) {
        $Env:BUILD_NUMBER = $rawver.Revision
    }
    elseif ($BuildNumber) {
        $Env:BUILD_NUMBER = $BuildNumber
    }
    else {
        $Env:BUILD_NUMBER = (Get-Date -Format Hmm)
    }

    $BuildArgs = @(
        (Resolve-Path "$SolutionDir\$Driver.sln"),
        "/m:4",
        "/p:Configuration=$ConfigurationBase $Configuration",
        "/p:Platform=$Platform",
        "/p:SignMode=Off",
        "/t:$Target",
        "/p:RunCodeAnalysis=true",
        "/p:EnablePREFast=true"
    )
    if ($Kasan) {
        $BuildArgs += @("/p:EnableKASAN=true")
    }

    msbuild.exe @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with error $LASTEXITCODE"
    }
}
finally {
    $Env:MAJOR_VERSION = ''
    $Env:MINOR_VERSION = ''
    $Env:MICRO_VERSION = ''
    $Env:BUILD_NUMBER = ''
    Pop-Location
}
