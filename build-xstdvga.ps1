[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x64", "arm64")]
    [string]$Platform,
    [Parameter()]
    [ValidateSet("vs2022")]
    [string]$SolutionDir = "vs2022"
)

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

$ErrorActionPreference = "Stop"

$Now = [datetime]::UtcNow
$DriverTime = $Now.ToString("Hmm")

Push-Location $PSScriptRoot\xstdvga
try {
    $RawVer = Get-PackageVersion -Raw xstdvga
    $CookedVer = Get-PackageVersion xstdvga
    $VerMajor = $CookedVer.Major
    $VerMinor = $CookedVer.Minor
    $VerBuild = $CookedVer.Build
    if ($RawVer.Revision -eq -1) {
        $VerRevision = $DriverTime
    }
    else {
        $VerRevision = $RawVer.Revision
    }
    $FinalVer = [version]::new($VerMajor, $VerMinor, $VerBuild, $VerRevision)
    $DriverVer = $Now.ToString("MM/dd/yyyy") + ",$FinalVer"

    .\build-dvl.ps1 `
        -Configuration $Configuration `
        -Platform $Platform `
        -SolutionDir $SolutionDir `
        -DriverVer $DriverVer

    Write-Host "Signing"
    Set-SignerFileSignature .\$SolutionDir\$Platform\$Configuration\xstdvga\xstdvga.sys, .\$SolutionDir\$Platform\$Configuration\xstdvga\xstdvga.cat
}
finally {
    Pop-Location
}
