[CmdletBinding()]
param (
    [Parameter()]
    [string]$Target = "Rebuild",
    [Parameter(Mandatory)]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [string]$Platform,
    [Parameter()]
    [string]$OutDir = "$PSScriptRoot\output",
    [Parameter()]
    [switch]$ExportCertificate,
    [Parameter()]
    [switch]$ExportSymbols,
    [Parameter()]
    [string]$ReleaseTag,
    [Parameter()]
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot/branding.ps1
. $PSScriptRoot/branding-generic.ps1
. $PSScriptRoot/scripts/sign.ps1

if (!$NoBuild) {
    msbuild.exe "$PSScriptRoot\installer\installer.sln" /t:$Target /restore /p:Configuration=$Configuration /p:Platform=$Platform
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with error $LASTEXITCODE"
    }
}

if ([string]::IsNullOrEmpty($ReleaseTag)) {
    $ReleaseTag = "$(Get-PackageVersion Product)-$Configuration-$Platform"
}

$VersionDir = "$OutDir\$ReleaseTag"
Remove-Item -Path $VersionDir -Force -Recurse -ErrorAction SilentlyContinue
if ($Target -ine "Clean") {
    $PackageDir = "$VersionDir\package"

    New-Item -Path $PackageDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\installer\bin\$Platform\$Configuration\en-US\*" -Exclude *.wixpdb -Destination $PackageDir\ -Force

    # XenClean
    $XenCleanDir = "$PackageDir\XenClean"
    New-Item -Path $XenCleanDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\Invoke-XenClean.ps1" -Destination $XenCleanDir\ -Force

    New-Item -Path $XenCleanDir\bin -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\*" -Exclude Invoke-XenClean.ps1, *.pdb -Destination $XenCleanDir\bin\ -Force

    # XenBootFix
    $XenBootFixDir = "$PackageDir\XenBootFix"
    New-Item -Path $XenBootFixDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenBootFix\$Platform\$Configuration\*" -Include *.exe -Destination $XenBootFixDir\ -Force

    if ($ExportCertificate) {
        $TestsignDir = "$VersionDir\testsign"

        New-Item -Path $TestsignDir -ItemType Directory -Force
        Copy-Item -Path "$PSScriptRoot\testsign\install.ps1" -Destination $TestsignDir\ -Force
        Export-SignerCertificate -OutDir $TestsignDir
    }

    if ($ExportSymbols) {
        $SymbolDir = "$VersionDir\symbols"
        New-Item -Path $SymbolDir -ItemType Directory -Force

        Copy-Item -Path "$PSScriptRoot\installer\bin\$Platform\$Configuration\en-US\*" -Filter *.wixpdb -Destination $SymbolDir\ -Force

        $DriversSymbolDir = "$VersionDir\symbols\drivers"
        New-Item -Path $DriversSymbolDir -ItemType Directory -Force
        Copy-Item -Path "$PSScriptRoot\installer\output\*\$Platform\*" -Filter *.pdb -Destination $DriversSymbolDir\ -Force

        $XenCleanSymbolDir = "$SymbolDir\XenClean"
        New-Item -Path $XenCleanSymbolDir -ItemType Directory -Force
        Copy-Item -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\*" -Filter *.pdb -Destination $XenCleanSymbolDir\ -Force

        $XenBootFixSymbolDir = "$SymbolDir\XenBootFix"
        New-Item -Path $XenBootFixSymbolDir -ItemType Directory -Force
        Copy-Item -Path "$PSScriptRoot\XenBootFix\$Platform\$Configuration\*" -Include *.pdb -Destination $XenBootFixSymbolDir\ -Force
    }
}
