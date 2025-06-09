[CmdletBinding()]
param (
    [Parameter()]
    [string[]]$Drivers = @("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd"),
    [Parameter()]
    [string]$Target = "Build",
    [Parameter(Mandatory)]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [string]$Platform,
    [Parameter()]
    [string]$SolutionDir = "vs2022",
    [Parameter()]
    [string]$ConfigurationBase = "Windows 10"
)

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

$OutputPath = "$PSScriptRoot\installer\output"
Remove-Item -Path $OutputPath -Force -Recurse -ErrorAction SilentlyContinue
$ErrorActionPreference = "Stop"
foreach ($repo in $Drivers) {
    Push-Location $PSScriptRoot\$repo
    try {
        $Env:MAJOR_VERSION = (Get-PackageVersion $repo).Major
        $Env:MINOR_VERSION = (Get-PackageVersion $repo).Minor
        $Env:MICRO_VERSION = (Get-PackageVersion $repo).Build
        $Env:BUILD_NUMBER = (Get-PackageVersion $repo).Revision

        git clean -fXd

        $DriverConfiguration = "$ConfigurationBase $Configuration"
        $DriverConfigShort = $DriverConfiguration.Replace(" ", "")

        $BuildArgs = @(
            (Resolve-Path "$SolutionDir\$repo.sln"),
            "/m:4",
            "/p:Configuration=$DriverConfiguration",
            "/p:Platform=$Platform",
            "/p:SignMode=Off",
            "/t:$Target"
        )

        msbuild.exe @BuildArgs
        if ($LASTEXITCODE -ne 0) {
            throw "MSBuild failed with error $LASTEXITCODE"
        }

        $DriverOutput = "$OutputPath\$repo\$Platform\$Configuration"
        New-Item -ItemType Directory -Path $DriverOutput -Force
        Copy-Item -Path .\$SolutionDir\$DriverConfigShort\$Platform\package\* -Destination $DriverOutput\ -Force -Recurse

        Set-SignerFileSignature $DriverOutput\*.sys, $DriverOutput\*.dll, $DriverOutput\*.exe, $DriverOutput\*.cat
    }
    finally {
        $Env:MAJOR_VERSION = ''
        $Env:MINOR_VERSION = ''
        $Env:MICRO_VERSION = ''
        $Env:BUILD_NUMBER = ''
        Pop-Location
    }
}
