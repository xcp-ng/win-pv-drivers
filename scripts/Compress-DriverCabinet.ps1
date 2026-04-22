param (
    [Parameter()]
    [string]$Path = "installer/driver-bins",
    [Parameter()]
    $OutPath = "output",
    [Parameter()]
    $OutName = "drivers.cab"
)

$ErrorActionPreference = "Stop"

$OutPath = Convert-Path $OutPath

$CabinetTemplate = @(
    ".OPTION EXPLICIT"
    ".Set DiskDirectoryTemplate=$OutPath"
    ".Set CabinetFileCountThreshold=0"
    ".Set FolderFileCountThreshold=0"
    ".Set FolderSizeThreshold=0"
    ".Set MaxCabinetSize=0"
    ".Set MaxDiskFileCount=0"
    ".Set MaxDiskSize=0"
    ".Set CompressionType=MSZIP"
    ".Set Cabinet=on"
    ".Set Compress=on"
)

Get-ChildItem -Directory $Path | ForEach-Object {
    $subdir = $_.Name
    $CabinetTemplate += @(".Set DestinationDir=$subdir")
    $_ | Get-ChildItem -Recurse -File | ForEach-Object {
        $CabinetTemplate += @($_.FullName)
    }
}

try {
    $TemplateFile = New-TemporaryFile
    Set-Content -Path $TemplateFile -Value $CabinetTemplate

    makecab.exe /F $TemplateFile /D CabinetNameTemplate=$OutName
}
finally {
    Remove-Item $TemplateFile
    Remove-Item setup.inf, setup.rpt
}
