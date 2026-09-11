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
    [string[]]$Include = @('*.sys', '*.exe', '*.dll'),
    [Parameter()]
    [string]$StagingRoot = "$PSScriptRoot\..\staging"
)

$ErrorActionPreference = 'Stop'

$result = @{}
$output = @{}
$fileCount = @{}
$failureCount = 0

foreach ($driver in $Drivers) {
    $driverPath = Join-Path $StagingRoot "$Platform\$Configuration\drivers\$driver"
    $catalogs = @(Get-ChildItem -LiteralPath $driverPath -File -Filter *.cat)
    $files = @(Get-ChildItem $driverPath\* -File -Include $Include | Sort-Object Name)

    if ($catalogs.Count -ne 1) {
        throw "Expected one catalog in '$driverPath', found $($catalogs.Count)"
    }
    if ($files.Count -eq 0) {
        throw "No included files found in '$driverPath'"
    }

    $catalog = $catalogs[0]
    $result[$driver] = @{}
    $output[$driver] = @{}
    $fileCount[$driver] = $files.Count

    foreach ($file in $files) {
        try {
            $ErrorActionPreference = 'Continue'
            $signToolOutput = & signtool.exe verify /q /kp /c $catalog.FullName $file.FullName 2>&1
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = 'Stop'
        }

        $output[$driver][$file.Name] = $signToolOutput
        $result[$driver][$file.Name] = $exitCode
        if ($result[$driver][$file.Name] -ne 0) {
            $failureCount++
        }
    }
}

$driverNameWidth = ($Drivers | Select-Object -ExpandProperty Length | Measure-Object -Maximum).Maximum
Write-Host (" " * ($driverNameWidth + 1) + "Files Result")
foreach ($driver in $Drivers) {
    $status = if (($result[$driver].Values | Where-Object { $_ -ne 0 }).Count -eq 0) { '....' } else { 'Fail' }
    Write-Host ($driver.PadRight($driverNameWidth) + " " + $fileCount[$driver].ToString().PadLeft(5) + " $status")
}

if ($failureCount -gt 0) {
    Write-Host
    foreach ($driver in $Drivers) {
        foreach ($file in $result[$driver].Keys) {
            if ($result[$driver][$file] -ne 0) {
                Write-Host "SignTool verification failed for ${driver}\${file}:"
                $output[$driver][$file] | Write-Host
                Write-Host
            }
        }
    }
    throw "Driver signature verification failed for $failureCount file(s)."
}
