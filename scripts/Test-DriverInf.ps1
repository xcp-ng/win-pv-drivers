[CmdletBinding()]
param (
    [Parameter()]
    [string[]]$Drivers = @("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd"),
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [string]$OutputPath = "$PSScriptRoot\..\installer\driver-bins",
    [Parameter()]
    [string]$DriverRequirementsBuild,
    [Parameter()]
    [string]$RuleVersion,
    [Parameter()]
    [ValidateSet("", "WHQL", "Universal", "Windows")]
    [string]$PrintErrors,
    [Parameter()]
    [switch]$Detailed
)

$ErrorActionPreference = 'Stop'

$result = @{}
$output = @{}

$PrintErrorsMap = @{
    WHQL      = "/h"
    Universal = "/u"
    Windows   = "/w"
}

foreach ($driver in $Drivers) {
    foreach ($mode in @("/h", "/u", "/w")) {
        $inf = "$OutputPath\$Platform\$Configuration\$driver\$driver.inf"

        if ($null -eq $result[$driver]) {
            $result[$driver] = @{}
        }
        if ($null -eq $output[$driver]) {
            $output[$driver] = @{}
        }

        if (!(Test-Path $inf)) {
            throw "Inf '$inf' doesn't exist"
        }

        $params = @()
        if ($DriverRequirementsBuild) {
            $params += @("/wbuild", $DriverRequirementsBuild)
        }
        if ($RuleVersion) {
            $params += @("/rulever", $RuleVersion)
        }
        if ($Detailed) {
            $params += @("/v")
        }

        $output[$driver][$mode] = (& "${Env:WindowsSdkDir}Tools\${Env:WindowsSDKLibVersion}\$Platform\infverif.exe" $mode $inf @params)
        $result[$driver][$mode] = $LASTEXITCODE
    }
}

$dnml = ($Drivers | Select-Object -ExpandProperty Length | Measure-Object -Maximum).Maximum
Write-Host (" " * ($dnml + 1) + "WHQL Univ Wind")
foreach ($driver in $Drivers) {
    $line = $driver.PadRight($dnml) + " "
    foreach ($mode in @("/h", "/u", "/w")) {
        if ($result[$driver][$mode] -eq 0) {
            $line += ".... "
        }
        else {
            $line += "Fail "
        }
    }
    Write-Host $line
}
if ($PrintErrors) {
    Write-Host
    $mode = $PrintErrorsMap[$PrintErrors]
    foreach ($driver in $Drivers) {
        if ($result[$driver][$mode] -ne 0) {
            Write-Host "InfVerif failures for ${driver}:"
            $output[$driver][$mode] | Write-Host
            Write-Host
        }
    }
}
