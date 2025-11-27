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
    [switch]$Sbom
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

if (!$NoBuild) {
    msbuild.exe `
        "$PSScriptRoot\installer\installer.sln" `
        /t:$Target `
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
        -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\Invoke-XenClean.ps1" `
        -Destination $XenCleanDir\ `
        -Force

    New-Item -Path $XenCleanDir\bin -ItemType Directory -Force
    Copy-Item `
        -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\*" `
        -Exclude Invoke-XenClean.ps1, *.pdb `
        -Destination $XenCleanDir\bin\ `
        -Force

    # XenBootFix
    $XenBootFixDir = "$PackageDir\XenBootFix"
    New-Item -Path $XenBootFixDir -ItemType Directory -Force
    Copy-Item `
        -Path "$PSScriptRoot\XenBootFix\$Platform\$Configuration\*" `
        -Include *.exe `
        -Destination $XenBootFixDir\ `
        -Force

    if ($Sbom) {
        & "$PSScriptRoot\scripts\Build-DriverSbom.ps1" -Configuration $Configuration -Platform $Platform
        Get-ChildItem -Directory -Filter _manifest -Recurse $PSScriptRoot\installer\output\ | ForEach-Object {
            $Driver = $($_.Parent.Name)
            $_ | Copy-Item -Destination $VersionDir\sbom\$Driver -Recurse
        }

        New-Item -Path $VersionDir\sbom\XenDriverUtils -ItemType Directory -Force
        sbom.exe generate `
            -b $PSScriptRoot\XenDriverUtils\bin\$Platform\$Configuration\net462 `
            -bc $PSScriptRoot\XenDriverUtils `
            -m $VersionDir\sbom\XenDriverUtils `
            -D true `
            -ps $Env:VENDOR_NAME `
            -pn XenDriverUtils `
            -pv (Get-PackageVersion Product)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool for XenDriverUtils failed with error $LASTEXITCODE"
        }

        New-Item -Path $VersionDir\sbom\XenClean -ItemType Directory -Force
        sbom.exe generate `
            -b $PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462 `
            -bc $PSScriptRoot\XenClean `
            -m $VersionDir\sbom\XenClean `
            -D true `
            -ps $Env:VENDOR_NAME `
            -pn XenClean `
            -pv (Get-PackageVersion XenClean)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool for XenClean failed with error $LASTEXITCODE"
        }

        New-Item -Path $VersionDir\sbom\XenBootFix -ItemType Directory -Force
        sbom.exe generate `
            -b $PSScriptRoot\XenBootFix\$Platform\$Configuration `
            -bc $PSScriptRoot\XenBootFix `
            -m $VersionDir\sbom\XenBootFix `
            -D true `
            -ps $Env:VENDOR_NAME `
            -pn XenBootFix `
            -pv (Get-PackageVersion XenBootFix)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool for XenBootFix failed with error $LASTEXITCODE"
        }

        # Syft is only used for xen-guest-agent (via cargo-auditable), since it's not possible to produce a sane SBOM
        # for it otherwise.
        New-Item -Path $VersionDir\sbom\xen-guest-agent -ItemType Directory -Force
        syft.exe scan `
            $PSScriptRoot\xen-guest-agent\target\release\xen-guest-agent.exe `
            -o "spdx-json@2.2=$VersionDir\sbom\xen-guest-agent\xen-guest-agent.json" `
            --source-supplier $Env:VENDOR_NAME `
            --source-name xen-guest-agent `
            --source-version (Get-PackageVersion XenGuestAgent)
        syft.exe scan `
            $PSScriptRoot\xen-guest-agent\target\release\xen-win-clipboard.exe `
            -o "spdx-json@2.2=$VersionDir\sbom\xen-guest-agent\xen-win-clipboard.json" `
            --source-supplier $Env:VENDOR_NAME `
            --source-name xen-win-clipboard `
            --source-version (Get-PackageVersion XenGuestAgent)

        New-Item -Path $VersionDir\sbom\xentimeprovider -ItemType Directory -Force
        sbom.exe generate `
            -b $PSScriptRoot\xentimeprovider\$Platform\$Configuration `
            -bc $PSScriptRoot\xentimeprovider `
            -m $VersionDir\sbom\xentimeprovider `
            -D true `
            -ps $Env:VENDOR_NAME `
            -pn xentimeprovider `
            -pv (Get-PackageVersion XenTimeProvider)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool for xentimeprovider failed with error $LASTEXITCODE"
        }

        New-Item -Path $VersionDir\sbom\DriverInstallCustomAction -ItemType Directory -Force
        sbom.exe generate `
            -b $PSScriptRoot\DriverInstallCustomAction\bin\$Platform\$Configuration\net462 `
            -bc $PSScriptRoot\DriverInstallCustomAction `
            -m $VersionDir\sbom\DriverInstallCustomAction `
            -D true `
            -ps $Env:VENDOR_NAME `
            -pn DriverInstallerCustomAction `
            -pv (Get-PackageVersion Product)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool for DriverInstallCustomAction failed with error $LASTEXITCODE"
        }

        New-Item -Path $VersionDir\sbom\win-pv-drivers-installer -ItemType Directory -Force
        sbom.exe generate `
            -b $PSScriptRoot\installer\bin\$Platform\$Configuration\en-US `
            -bc $PSScriptRoot\installer\ `
            -m $VersionDir\sbom\win-pv-drivers-installer `
            -D true `
            -ps $Env:VENDOR_NAME `
            -pn win-pv-drivers-installer `
            -pv (Get-PackageVersion Product)
        if ($LASTEXITCODE -ne 0) {
            throw "sbom-tool for win-pv-drivers-installer failed with error $LASTEXITCODE"
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
            -Path "$PSScriptRoot\installer\output\$Platform\$Configuration\*\*" `
            -Filter *.pdb `
            -Destination $DriversSymbolDir\ `
            -Force

        $XenCleanSymbolDir = "$SymbolDir\XenClean"
        New-Item -Path $XenCleanSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\*" `
            -Filter *.pdb `
            -Destination $XenCleanSymbolDir\ `
            -Force

        $XenBootFixSymbolDir = "$SymbolDir\XenBootFix"
        New-Item -Path $XenBootFixSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$PSScriptRoot\XenBootFix\$Platform\$Configuration\*" `
            -Include *.pdb `
            -Destination $XenBootFixSymbolDir\ `
            -Force

        $XenGuestAgentSymbolDir = "$SymbolDir\xen-guest-agent"
        New-Item -Path $XenGuestAgentSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$PSScriptRoot\xen-guest-agent\target\$Configuration\*" `
            -Include *.pdb `
            -Destination $XenGuestAgentSymbolDir\ `
            -Force

        $XenTimeProviderSymbolDir = "$SymbolDir\xentimeprovider"
        New-Item -Path $XenTimeProviderSymbolDir -ItemType Directory -Force
        Copy-Item `
            -Path "$PSScriptRoot\xentimeprovider\$Platform\$Configuration\*" `
            -Include *.pdb `
            -Destination $XenTimeProviderSymbolDir\ `
            -Force
    }
}
