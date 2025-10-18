[CmdletBinding()]
param (
    [Parameter()]
    [string[]]$Drivers = @("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd"),
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
    [string]$ConfigurationBase = "Windows 10"
)

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

# Drivers are ordered by build date first so the HHmm gives you a more granular revision number (down to the minute).
# The "1" is to avoid leading zeroes.
$DriverTime = "1" + (Get-Date -Format HHmm)

$OutputPath = "$PSScriptRoot\installer\output"
Remove-Item -Path $OutputPath -Force -Recurse -ErrorAction SilentlyContinue
$ErrorActionPreference = "Stop"
foreach ($repo in $Drivers) {
    Push-Location $PSScriptRoot\$repo
    try {
        $rawver = Get-PackageVersion -Raw $repo
        $ver = Get-PackageVersion $repo
        $Env:MAJOR_VERSION = $ver.Major
        $Env:MINOR_VERSION = $ver.Minor
        $Env:MICRO_VERSION = $ver.Build
        if ($rawver.Revision -eq -1) {
            $Env:BUILD_NUMBER = $DriverTime
        }
        else {
            $Env:BUILD_NUMBER = $rawver.Revision
        }

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

        $DriverOutput = "$OutputPath\$Platform\$Configuration\$repo"
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
