[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$TrustedPath,
    [Parameter(Mandatory)]
    [string]$ComparePath,
    [Parameter()]
    [string]$Filter,
    [Parameter()]
    [string[]]$Include,
    [Parameter()]
    [string[]]$Exclude
)

$ErrorActionPreference = 'Stop'

function Get-ArtifactCatalog {
    param (
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter()]
        [string]$Filter,
        [Parameter()]
        [string[]]$Include,
        [Parameter()]
        [string[]]$Exclude
    )

    try {
        Push-Location $Path | Out-Null
        return (Get-ChildItem -Recurse -File -Filter $Filter -Include $Include -Exclude $Exclude |
            Resolve-Path -Relative |
            ForEach-Object {
                [PSCustomObject]@{
                    Path = $_
                    Hash = (Get-AppLockerFileInformation $_).Hash.ToString()
                }
            })
    }
    finally {
        Pop-Location | Out-Null
    }
}

$trustedCatalog = Get-ArtifactCatalog -Path $TrustedPath -Filter $Filter -Include $Include -Exclude $Exclude
$compareCatalog = Get-ArtifactCatalog -Path $ComparePath -Filter $Filter -Include $Include -Exclude $Exclude

$differences = Compare-Object -ReferenceObject $trustedCatalog -DifferenceObject $compareCatalog -Property Path, Hash
if ($differences) {
    Write-Warning "The two catalogs differ"
    $differences | Write-Warning
    return $false
}

return $true
