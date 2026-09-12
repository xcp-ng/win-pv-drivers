[CmdletBinding()]
param (
    [Parameter()]
    [string]$Target = "Rebuild",
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [string]$OutDir = "$PSScriptRoot\output",
    [Parameter()]
    [switch]$ExportCertificate,
    [Parameter()]
    [switch]$ExportSymbols,
    [Parameter()]
    [switch]$ExportExtras,
    [Parameter()]
    [string]$ReleaseTag,
    [Parameter()]
    [switch]$NoBuild,
    [Parameter()]
    [switch]$Sbom,
    [Parameter()]
    [switch]$Zip,
    [Parameter()]
    [switch]$Iso
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

$StagingDir = "$PSScriptRoot\staging\$Platform\$Configuration"
$DriversDir = "$StagingDir\drivers"
$ComponentsDir = "$StagingDir\components"
$XenplusDir = "$StagingDir\xenplus"
$XstdvgaDir = "$StagingDir\xstdvga"

if (!$NoBuild) {
    msbuild.exe `
        "$PSScriptRoot\installer\XenDrivers.wixproj" `
        /restore `
        /p:Configuration=$Configuration `
        /p:Platform=$Platform
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with error $LASTEXITCODE"
    }
}

if ([string]::IsNullOrEmpty($ReleaseTag)) {
    $ReleaseTag = "xcpng-winpv-$(Get-PackageVersion Product)-$Configuration-$Platform"
}
if ($Env:GITHUB_ACTIONS) {
    Add-Content -Path $Env:GITHUB_OUTPUT -Value "ReleaseTag=$ReleaseTag" -Force
    Add-Content -Path $Env:GITHUB_STEP_SUMMARY -Value "ReleaseTag: ``$ReleaseTag``" -Force
}

$VersionDir = "$OutDir\$ReleaseTag"
Remove-Item -Path $VersionDir -Force -Recurse -ErrorAction SilentlyContinue
if ($Target -ine "Clean") {
    $PackageDir = "$VersionDir\package"
    New-Item -Path $PackageDir -ItemType Directory -Force
    Copy-Item `
        -Path "$PSScriptRoot\installer\bin\$Platform\$Configuration\en-US\*" `
        -Exclude *.wixpdb `
        -Destination $PackageDir\ `
        -Force

    # XenClean
    $XenCleanDir = "$PackageDir\XenClean"
    New-Item -Path $XenCleanDir -ItemType Directory -Force
    Copy-Item `
        -Path "$ComponentsDir\XenClean\*" `
        -Include *.exe `
        -Destination $XenCleanDir\ `
        -Force

    # XenBootFix
    $XenBootFixDir = "$PackageDir\XenBootFix"
    New-Item -Path $XenBootFixDir -ItemType Directory -Force
    Copy-Item `
        -Path "$ComponentsDir\XenBootFix\*" `
        -Include *.exe `
        -Destination $XenBootFixDir\ `
        -Force

    if ($Sbom) {
        $SbomDir = "$VersionDir\sbom"
        New-Item -Path $SbomDir -ItemType Directory -Force | Out-Null
        $SbomComponents = @(
            [PSCustomObject]@{
                ComponentName = "xenbus"
                SourcePath    = "$PSScriptRoot\xenbus"
                BinaryPath    = "$DriversDir\xenbus"
                Version       = (Get-PackageVersion xenbus)
            }
            [PSCustomObject]@{
                ComponentName = "xencons"
                SourcePath    = "$PSScriptRoot\xencons"
                BinaryPath    = "$DriversDir\xencons"
                Version       = (Get-PackageVersion xencons)
            }
            [PSCustomObject]@{
                ComponentName = "xenhid"
                SourcePath    = "$PSScriptRoot\xenhid"
                BinaryPath    = "$DriversDir\xenhid"
                Version       = (Get-PackageVersion xenhid)
            }
            [PSCustomObject]@{
                ComponentName = "xeniface"
                SourcePath    = "$PSScriptRoot\xeniface"
                BinaryPath    = "$DriversDir\xeniface"
                Version       = (Get-PackageVersion xeniface)
            }
            [PSCustomObject]@{
                ComponentName = "xennet"
                SourcePath    = "$PSScriptRoot\xennet"
                BinaryPath    = "$DriversDir\xennet"
                Version       = (Get-PackageVersion xennet)
            }
            [PSCustomObject]@{
                ComponentName = "xenvbd"
                SourcePath    = "$PSScriptRoot\xenvbd"
                BinaryPath    = "$DriversDir\xenvbd"
                Version       = (Get-PackageVersion xenvbd)
            }
            [PSCustomObject]@{
                ComponentName = "xenvif"
                SourcePath    = "$PSScriptRoot\xenvif"
                BinaryPath    = "$DriversDir\xenvif"
                Version       = (Get-PackageVersion xenvif)
            }
            [PSCustomObject]@{
                ComponentName = "xenvkbd"
                SourcePath    = "$PSScriptRoot\xenvkbd"
                BinaryPath    = "$DriversDir\xenvkbd"
                Version       = (Get-PackageVersion xenvkbd)
            }
            [PSCustomObject]@{
                ComponentName = "XenDriverUtils"
                SourcePath    = "$PSScriptRoot\XenDriverUtils"
                BinaryPath    = "$ComponentsDir\XenDriverUtils"
                Version       = (Get-PackageVersion Product)
            }
            [PSCustomObject]@{
                ComponentName = "XenClean"
                SourcePath    = "$PSScriptRoot\XenClean"
                BinaryPath    = "$ComponentsDir\XenClean"
                Version       = (Get-PackageVersion XenClean)
            }
            [PSCustomObject]@{
                ComponentName = "XenBootFix"
                SourcePath    = "$PSScriptRoot\XenBootFix"
                BinaryPath    = "$ComponentsDir\XenBootFix"
                Version       = (Get-PackageVersion XenBootFix)
            }
            [PSCustomObject]@{
                ComponentName = "xenplus"
                SourcePath    = "$PSScriptRoot\xenplus"
                BinaryPath    = $XenplusDir
                Version       = (Get-PackageVersion xenplus)
            }
            [PSCustomObject]@{
                ComponentName = "xstdvga"
                SourcePath    = "$PSScriptRoot\xstdvga\vs2022"
                BinaryPath    = $XstdvgaDir
                Version       = (Get-PackageVersion xstdvga)
            }
            [PSCustomObject]@{
                ComponentName = "DriverInstallCustomAction"
                SourcePath    = "$PSScriptRoot\DriverInstallCustomAction"
                BinaryPath    = "$ComponentsDir\DriverInstallCustomAction"
                Version       = (Get-PackageVersion Product)
            }
            [PSCustomObject]@{
                ComponentName = "win-pv-drivers-installer"
                SourcePath    = "$PSScriptRoot\installer"
                BinaryPath    = "$PSScriptRoot\installer\bin\$Platform\$Configuration\en-US"
                Version       = (Get-PackageVersion Product)
            }
        )

        foreach ($component in $SbomComponents) {
            $SbomPath = "$SbomDir\$($component.ComponentName).spdx.json"
            $ManifestDir = "$SbomDir\$($component.ComponentName)"
            $GeneratedSbomPath = "$ManifestDir\_manifest\spdx_2.2\manifest.spdx.json"
            Remove-Item -Path $SbomPath -Force -ErrorAction SilentlyContinue
            Remove-Item -Path $ManifestDir -Recurse -Force -ErrorAction SilentlyContinue
            New-Item -Path $ManifestDir -ItemType Directory -Force | Out-Null

            $sbomArgs = @(
                "generate",
                "-b", $component.BinaryPath,
                "-bc", $component.SourcePath,
                "-m", $ManifestDir,
                "-D", "true",
                "-ps", $Env:VENDOR_NAME,
                "-pn", $component.ComponentName,
                "-pv", $component.Version,
                "-V", "error"
            )

            try {
                & sbom.exe @sbomArgs
                if ($LASTEXITCODE -ne 0) {
                    throw "sbom-tool for $($component.ComponentName) failed with error $LASTEXITCODE"
                }
                if (-not (Test-Path -LiteralPath $GeneratedSbomPath -PathType Leaf)) {
                    throw "sbom-tool did not produce $GeneratedSbomPath"
                }
                Copy-Item -LiteralPath $GeneratedSbomPath -Destination $SbomPath -Force
            }
            finally {
                Remove-Item -Path $ManifestDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($ExportExtras) {
        $ExtrasDir = "$VersionDir\extras"
        New-Item -Path $ExtrasDir -ItemType Directory -Force
        Copy-Item -Path "$PSScriptRoot\extras\*" -Destination $ExtrasDir\ -Force
    }

    if ($ExportCertificate) {
        $TestsignDir = "$VersionDir\testsign"

        New-Item -Path $TestsignDir -ItemType Directory -Force
        Copy-Item -Path "$PSScriptRoot\testsign\install.ps1" -Destination $TestsignDir\ -Force
        Export-SignerCertificate -OutDir $TestsignDir
    }

    if ($ExportSymbols) {
        $SymbolDir = "$VersionDir\symbols"
        New-Item -Path $SymbolDir -ItemType Directory -Force

        Copy-Item `
            -Path "$PSScriptRoot\installer\bin\$Platform\$Configuration\en-US\*" `
            -Filter *.wixpdb `
            -Destination $SymbolDir\ `
            -Force

        $DriversSymbolDir = "$VersionDir\symbols\drivers"
        New-Item -Path $DriversSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$DriversDir\*\*" `
            -Filter *.pdb `
            -Destination $DriversSymbolDir\ `
            -Force

        $XenCleanSymbolDir = "$SymbolDir\XenClean"
        New-Item -Path $XenCleanSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$ComponentsDir\XenClean\*" `
            -Filter *.pdb `
            -Destination $XenCleanSymbolDir\ `
            -Force

        $XenBootFixSymbolDir = "$SymbolDir\XenBootFix"
        New-Item -Path $XenBootFixSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$ComponentsDir\XenBootFix\*" `
            -Include *.pdb `
            -Destination $XenBootFixSymbolDir\ `
            -Force

        $XenplusSymbolDir = "$SymbolDir\xenplus"
        New-Item -Path $XenplusSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$XenplusDir\*" `
            -Include *.pdb `
            -Destination $XenplusSymbolDir\ `
            -Force

        $XstdvgaSymbolDir = "$SymbolDir\xstdvga"
        New-Item -Path $XstdvgaSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$XstdvgaDir\*" `
            -Include *.pdb `
            -Destination $XstdvgaSymbolDir\ `
            -Force
    }

    if ($Zip) {
        # Compress-Archive (and .NET zip support in general) has a bug that causes path separators to be non-compliant:
        # https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/mitigation-ziparchiveentry-fullname-path-separator
        # So use Windows's bsdtar instead.
        $tar = Join-Path ([System.Environment]::SystemDirectory) "tar.exe"
        Push-Location $OutDir
        try {
            & $tar --format zip -cf "$OutDir\$ReleaseTag.zip" $ReleaseTag
            if ($LASTEXITCODE -ne 0) {
                throw "tar failed with error $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($Iso) {
        & "$PSScriptRoot\scripts\New-IsoImage.ps1" `
            -Path $VersionDir `
            -ImageFilePath "$OutDir\$ReleaseTag.iso" `
            -VolumeLabel $ReleaseTag
    }
}
