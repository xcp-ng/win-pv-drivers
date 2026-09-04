[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [string]$InputDir = "$PSScriptRoot\input",
    [Parameter()]
    [string]$Drivers = "$PSScriptRoot\driver-bins\$Platform\$Configuration",
    [Parameter()]
    [string]$Components = "$PSScriptRoot\components\$Platform\$Configuration",
    [Parameter()]
    [string]$Xenplus = "$PSScriptRoot\xenplus\bin\publish\$Platform\$Configuration",
    [Parameter()]
    [string]$Xstdvga = "$PSScriptRoot\xstdvga\vs2022\$Platform\$Configuration\xstdvga"
)

$ErrorActionPreference = "Stop"

$tar = Join-Path ([System.Environment]::SystemDirectory) "tar.exe"

$Artifacts = [ordered]@{
    drivers    = $Drivers
    components = $Components
    xenplus    = $Xenplus
    xstdvga    = $Xstdvga
}

$InputDir = (New-Item -Path $InputDir -ItemType Directory -Force).FullName

foreach ($Artifact in $Artifacts.GetEnumerator()) {
    if (!(Test-Path $Artifact.Value -PathType Container)) {
        throw "$($Artifact.Key) build output '$($Artifact.Value)' doesn't exist"
    }

    $Archive = Join-Path $InputDir "$($Artifact.Key)-local.zip"
    Remove-Item $Archive -Force -ErrorAction SilentlyContinue

    $Entries = Get-ChildItem -LiteralPath $Artifact.Value -Force | Select-Object -ExpandProperty Name
    if (!$Entries) {
        throw "$($Artifact.Key) build output '$($Artifact.Value)' is empty"
    }

    & $tar --format zip -cf $Archive -C $Artifact.Value $Entries
    if ($LASTEXITCODE -ne 0) {
        throw "archiving $($Artifact.Key) failed with error $LASTEXITCODE"
    }

    Write-Output "Created $Archive"
}
