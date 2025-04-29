$ErrorActionPreference = 'Stop'

function Get-ArtifactCatalog {
    [CmdletBinding()]
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
                    Path             = $_
                    AuthenticodeHash = (Get-AppLockerFileInformation $_).Hash.ToString()
                }
            })
    }
    finally {
        Pop-Location | Out-Null
    }
}

function Compare-Artifact {
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

    $trustedCatalog = Get-ArtifactCatalog -Path $TrustedPath -Filter $Filter -Include $Include -Exclude $Exclude
    $compareCatalog = Get-ArtifactCatalog -Path $ComparePath -Filter $Filter -Include $Include -Exclude $Exclude

    $differences = Compare-Object -ReferenceObject $trustedCatalog -DifferenceObject $compareCatalog -Property Path, AuthenticodeHash
    if ($differences) {
        Write-Warning "The two catalogs differ"
        $differences | Write-Warning
        return $false
    }

    return $true
}

function Test-ArtifactCatalog {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [object[]]$TrustedCatalog,
        [Parameter(Mandatory)]
        [string]$ComparePath,
        [Parameter(Mandatory)]
        [string]$CatalogPrefix,
        [Parameter()]
        [string]$Filter,
        [Parameter()]
        [string[]]$Include,
        [Parameter()]
        [string[]]$Exclude
    )

    $filteredTrustedCatalog = $TrustedCatalog | Where-Object {
        $_.Path.StartsWith($CatalogPrefix, [System.StringComparison]::OrdinalIgnoreCase)
    }

    $compareCatalog = Get-ArtifactCatalog -Path $ComparePath -Filter $Filter -Include $Include -Exclude $Exclude |
    ForEach-Object {
        $compareFile = Join-Path $CatalogPrefix $_.Path.TrimStart([char]'.', [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        Write-Verbose "$($_.Path) -> $compareFile"
        [PSCustomObject]@{
            Path             = $compareFile
            AuthenticodeHash = $_.AuthenticodeHash
        }
    }

    Compare-Object -ReferenceObject $filteredTrustedCatalog -DifferenceObject $compareCatalog -Property Path, AuthenticodeHash
}
