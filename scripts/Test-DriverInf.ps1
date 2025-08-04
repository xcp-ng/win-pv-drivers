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
    [string]$OutputPath = "$PSScriptRoot\..\installer\output"
)

$ErrorActionPreference = 'Stop'

$result = @{}

foreach ($driver in $Drivers) {
    foreach ($mode in @("/h", "/u", "/w")) {
        $inf = "$OutputPath\$driver\$Platform\$Configuration\$driver.inf"

        if ($null -eq $result[$driver]) {
            $result[$driver] = @{}
        }

        if (!(Test-Path $inf)) {
            throw "Inf '$inf' doesn't exist"
        }

        & "${Env:WindowsSdkDir}Tools\${Env:WindowsSDKLibVersion}\$Platform\infverif.exe" $mode $inf > $null
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
