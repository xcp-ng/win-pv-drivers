<#
.SYNOPSIS
Generate an SPDX 3.0.1 build SBOM for one driver package (INF + binaries).

.DESCRIPTION
The SBOM follows the SPDX 3.0.1 mapping of BSI TR-03183-2 v2.1.0 (appendix 8.2), which the WHCP SBOM requirement is
based on:

    driver package (the component)
      contains       -> its files: INF, catalog, binaries (SHA-512, BSI properties)
                        statically linked kits (WDK / SDK / MSVC runtime)
      dependsOn      -> Microsoft Windows, other driver packages
      generated from -> source repository at the built commit

Facts come from three places:
- sbom-components.json: licences, kits linked into each kind of binary, dependencies between driver packages,
  minimum Windows version.
- The build: the INF, dumpbin on each binary (kernel/user mode), MSBuild/NuGet for the toolchain versions, git for
  the source commit.
- Branding: VENDOR_NAME and PRODUCT_URL (name and URL of the creator), and SBOM_NAMESPACE (root of the element IRIs).
  PRODUCT_URL and SBOM_NAMESPACE are independent and both required; neither stands in for the other.

When a fact about the build environment cannot be determined, a warning is printed and the SBOM is still written;
with -Strict this is an error instead. Broken inputs always fail.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9]{1,32}$')]
    [string]$Driver,
    # Directory containing the built driver package (INF, catalog and binaries).
    [Parameter(Mandatory)]
    [string]$PackagePath,
    [Parameter(Mandatory)]
    [string]$OutFile,
    [Parameter()]
    [ValidateSet("x86", "x64", "arm64")]
    [string]$Platform = "x64",
    # Project of the driver, used to query the toolchain through MSBuild.
    [Parameter()]
    [string]$MSBuildProject,
    # MSBuild Configuration of the build, e.g. "Windows 10 Release".
    [Parameter()]
    [string]$Configuration,
    [Parameter()]
    [switch]$Strict
)

$ErrorActionPreference = "Stop"

$ComponentsFile = Join-Path $PSScriptRoot "sbom-components.json"
# Git checkout of the driver sources: the driver submodule.
$SourcePath = Join-Path $PSScriptRoot "..\$Driver"

. $PSScriptRoot\..\branding.ps1
. $PSScriptRoot\branding-generic.ps1

$VersionPattern = '^[0-9]+(\.[0-9]+){1,3}$'
$NoneElement = "https://spdx.org/rdf/3.0.1/terms/Core/NoneElement"
$NoAssertionElement = "https://spdx.org/rdf/3.0.1/terms/Core/NoAssertionElement"

$Problems = New-Object System.Collections.Generic.List[string]
function Add-Problem {
    param ([Parameter(Mandatory)][string]$Message)
    if ($Strict) {
        $Problems.Add($Message)
    }
    else {
        Write-Warning "SBOM ${Driver}: $Message"
    }
}

function Invoke-Tool {
    # Run a native tool; return its trimmed stdout, or $null on failure. Arguments are passed as an array.
    param (
        [Parameter(Mandatory)][string]$Command,
        [string[]]$Arguments
    )
    $exe = Get-Command $Command -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$exe) {
        return $null
    }
    $ErrorActionPreference = "Continue"
    $output = & $exe.Source @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    return (@($output) -join "`n").Trim()
}

#region 1. Facts about the build

function Get-PackageInfo {
    # Version (DriverVer) and files of the driver package: the INF, its catalog and its [SourceDisksFiles].
    $infPath = Join-Path $PackagePath "$Driver.inf"
    $arch = @{
        x86   = "x86"
        x64   = "amd64"
        arm64 = "arm64"
    }[$Platform]
    $section = $null
    $version = @{}
    $files = New-Object System.Collections.Generic.List[string]
    $files.Add("$Driver.inf")

    foreach ($rawLine in [System.IO.File]::ReadAllLines($infPath)) {
        $line = ($rawLine -replace ';.*$', '').Trim()
        if ($line -match '^\[(.+)\]$') {
            $section = $Matches[1].Trim()
        }
        elseif ($section -eq "Version" -and $line -match '^([A-Za-z.0-9]+)\s*=\s*(.*)$') {
            $version[$Matches[1]] = $Matches[2].Trim().Trim('"')
        }
        elseif ($line -and $section -match '^SourceDisksFiles(\.(.+))?$' -and (!$Matches[2] -or $Matches[2] -eq $arch)) {
            $files.Add(($line -split '=')[0].Trim().Trim('"'))
        }
    }

    $catalog = $version["CatalogFile.NT$arch"]
    if (!$catalog) {
        $catalog = $version["CatalogFile"]
    }
    if ($catalog) {
        $files.Insert(1, $catalog)
    }
    $packageVersion = (@($version["DriverVer"] -split ',') + @(""))[1].Trim()
    if ($packageVersion -notmatch $VersionPattern) {
        throw "${infPath}: missing or invalid DriverVer"
    }
    $names = @($files | Select-Object -Unique)
    foreach ($name in $names) {
        if (!(Test-Path -LiteralPath (Join-Path $PackagePath $name) -PathType Leaf)) {
            throw "${infPath}: '$name' is not a file of the package"
        }
    }
    return [pscustomobject]@{
        Version = $packageVersion
        Files   = $names
    }
}

function Get-BinaryInfo {
    # From dumpbin: kernel/user mode, and whether the image has code (resource-only images link nothing).
    param ([Parameter(Mandatory)][string]$Path)

    $arguments = @(
        "/nologo",
        "/headers",
        $Path
    )
    $previousLanguage = $Env:VSLANG
    $Env:VSLANG = "1033" # the parser expects English output
    try {
        $output = Invoke-Tool $Toolchain.Dumpbin $arguments
    }
    finally {
        $Env:VSLANG = $previousLanguage
    }
    if (!$output -or !($output -match '(?m)^\s*([0-9A-Fa-f]+) subsystem \(')) {
        throw "${Path}: cannot read the PE headers with dumpbin '$($Toolchain.Dumpbin)'"
    }
    $kernelMode = [Convert]::ToInt32($Matches[1], 16) -eq 1 # IMAGE_SUBSYSTEM_NATIVE
    $kind = "userMode"
    if ($kernelMode) {
        $kind = "kernelMode"
    }
    return [pscustomobject]@{
        Kind    = $kind
        HasCode = $output -match '(?m)^\s+(Code|Execute\b.*)$'
    }
}

function Get-Toolchain {
    # Kit and toolset versions (with PURLs for NuGet kits) and the dumpbin of the toolset used by the build.
    $toolchain = [pscustomobject]@{
        wdk     = [pscustomobject]@{
            Version = $null
            Purl    = $null
        }
        sdk     = [pscustomobject]@{
            Version = $null
            Purl    = $null
        }
        msvc    = [pscustomobject]@{
            Version = $null
            Purl    = $null
        }
        Dumpbin = "dumpbin.exe"
    }
    if (!$MSBuildProject) {
        Add-Problem "no MSBuild project given; toolchain versions are unknown"
        return $toolchain
    }

    # Kits consumed through NuGet: packages.config next to the project or in its parent.
    $arch = @{
        x86   = "x86"
        x64   = "x64"
        arm64 = "arm64"
    }[$Platform]
    $projectDir = Split-Path -Parent $MSBuildProject
    $configs = @(
        (Join-Path $projectDir "packages.config"),
        (Join-Path (Split-Path -Parent $projectDir) "packages.config")
    )
    $kits = @(
        @{
            Name = "wdk"
            Id   = "Microsoft.Windows.WDK.$arch"
        },
        @{
            Name = "sdk"
            Id   = "Microsoft.Windows.SDK.CPP.$arch"
        }
    )
    foreach ($config in $configs) {
        if (!(Test-Path -LiteralPath $config -PathType Leaf)) {
            continue
        }
        $settings = New-Object System.Xml.XmlReaderSettings
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $reader = [System.Xml.XmlReader]::Create($config, $settings)
        try {
            $xml = New-Object System.Xml.XmlDocument
            $xml.Load($reader)
        }
        finally {
            $reader.Dispose()
        }
        foreach ($kit in $kits) {
            $package = $xml.SelectNodes("/packages/package") | Where-Object { $_.id -eq $kit.Id } | Select-Object -First 1
            if ($package -and !$toolchain.($kit.Name).Version) {
                $toolchain.($kit.Name).Version = $package.version
                $toolchain.($kit.Name).Purl = "pkg:nuget/$($kit.Id)@$($package.version)"
            }
        }
    }

    # Installed kits and MSVC toolset, as resolved by MSBuild for this build.
    $arguments = @(
        "-nologo",
        $MSBuildProject,
        "-p:Platform=$Platform",
        "-p:Configuration=$Configuration",
        "-getProperty:TargetPlatformVersion",
        "-getProperty:VCToolsVersion",
        "-getProperty:VCToolsInstallDir"
    )
    $properties = $null
    try {
        $properties = (Invoke-Tool msbuild.exe $arguments | ConvertFrom-Json).Properties
    }
    catch {
    }
    if (!$properties) {
        Add-Problem "cannot query the toolchain of '$MSBuildProject' with msbuild.exe"
        return $toolchain
    }
    foreach ($kit in "wdk", "sdk") {
        if (!$toolchain.$kit.Version) {
            # The WDK and the SDK it builds against share the target platform version.
            $toolchain.$kit.Version = $properties.TargetPlatformVersion
        }
    }
    $toolchain.msvc.Version = $properties.VCToolsVersion
    $dumpbin = Join-Path $properties.VCToolsInstallDir "bin\Hostx64\x64\dumpbin.exe"
    if (Test-Path -LiteralPath $dumpbin -PathType Leaf) {
        $toolchain.Dumpbin = $dumpbin
    }
    return $toolchain
}

function Get-SourceLocator {
    # git+<submodule URL>@<built commit>, or $null.
    $urlArguments = @(
        "-C", "$PSScriptRoot\..",
        "config",
        "-f", ".gitmodules",
        "--get", "submodule.$Driver.url"
    )
    $commitArguments = @(
        "-C", $SourcePath,
        "rev-parse",
        "HEAD"
    )
    $statusArguments = @(
        "-C", $SourcePath,
        "status",
        "--porcelain",
        "--untracked-files=no"
    )

    $url = Invoke-Tool git $urlArguments
    $commit = Invoke-Tool git $commitArguments
    if ($url -notmatch '^https://[A-Za-z0-9.-]+/[A-Za-z0-9._~/-]+$' -or $commit -notmatch '^[0-9a-f]{40}$') {
        Add-Problem "cannot determine the source URL and commit of '$SourcePath'"
        return $null
    }
    if (Invoke-Tool git $statusArguments) {
        Add-Problem "'$SourcePath' has uncommitted changes; the source URI does not describe the built code"
    }
    return "git+$url@$commit"
}

#endregion

#region 2. SPDX graph

$Graph = New-Object System.Collections.Generic.List[object]
$LicenseIds = @{}
$ExternalIds = @{}

function Add-Element {
    param ([Parameter(Mandatory)]$Element)
    $Element.creationInfo = "_:creationinfo"
    $Graph.Add($Element)
}

function Add-Relationship {
    param (
        [Parameter(Mandatory)][string]$From,
        [Parameter(Mandatory)][string]$Type,
        [string[]]$To = @($NoneElement),
        [string]$Completeness = "complete"
    )
    Add-Element ([ordered]@{
            type             = "Relationship"
            spdxId           = "$Base#rel-$($Graph.Count + 1)"
            from             = $From
            relationshipType = $Type
            to               = [string[]]$To
            completeness     = $Completeness
        })
}

function Add-Organization {
    param (
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Url
    )
    Add-Element ([ordered]@{
            type               = "Organization"
            spdxId             = $Id
            name               = $Name
            externalIdentifier = @([ordered]@{
                    type                   = "ExternalIdentifier"
                    externalIdentifierType = "urlScheme"
                    identifier             = $Url
                })
        })
}

function Add-Package {
    # A component. Distribution licence -> hasConcludedLicense, original licence -> hasDeclaredLicense (BSI table 9/11).
    param (
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$Creator,
        [Parameter(Mandatory)][string]$Purpose,
        [string]$License,
        [string]$Purl
    )
    $element = [ordered]@{
        type                    = "software_Package"
        spdxId                  = $Id
        name                    = $Name
        software_packageVersion = $Version
        originatedBy            = @($Creator)
        software_primaryPurpose = $Purpose
    }
    if ($Purl) {
        $element.externalIdentifier = @([ordered]@{
                type                   = "ExternalIdentifier"
                externalIdentifierType = "packageUrl"
                identifier             = $Purl
            })
    }
    Add-Element $element

    if ($License) {
        if (!$LicenseIds.ContainsKey($License)) {
            $LicenseIds[$License] = "$Base#license-$($LicenseIds.Count + 1)"
            Add-Element ([ordered]@{
                    type                               = "simplelicensing_LicenseExpression"
                    spdxId                             = $LicenseIds[$License]
                    simplelicensing_licenseExpression  = $License
                    simplelicensing_licenseListVersion = $Declaration.LicenseListVersion
                })
        }
        Add-Relationship -From $Id -Type hasConcludedLicense -To $LicenseIds[$License]
        Add-Relationship -From $Id -Type hasDeclaredLicense -To $LicenseIds[$License]
    }
}

function Add-File {
    # BSI properties through software_additionalPurpose: "executable" if executable, "firmware" if unstructured (all of
    # our files are). None are archives.
    param (
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Path,
        [switch]$Executable
    )
    $purposes = @("firmware")
    if ($Executable) {
        $purposes = @("executable", "firmware")
    }
    Add-Element ([ordered]@{
            type                       = "software_File"
            spdxId                     = $Id
            name                       = Split-Path -Leaf $Path
            software_additionalPurpose = $purposes
            comment                    = "software_additionalPurpose field is used to indicate the properties of BSI TR-03183-2"
            verifiedUsing              = @([ordered]@{
                    type      = "Hash"
                    algorithm = "sha512"
                    hashValue = (Get-FileHash -LiteralPath $Path -Algorithm SHA512).Hash.ToLowerInvariant()
                })
        })
}

function Get-ExternalComponentId {
    # Kits and Windows from the declaration, added on first use. Their own dependencies are not resolved.
    param ([Parameter(Mandatory)][string]$Key)
    if (!$ExternalIds.ContainsKey($Key)) {
        $component = $Declaration.components.$Key
        if (!$component) {
            throw "${ComponentsFile}: no component '$Key'"
        }
        $creatorId = "$Base#agent-$($component.Creator)"
        if (!($Graph | Where-Object { $_.spdxId -eq $creatorId })) {
            $agent = $Declaration.agents.($component.Creator)
            Add-Organization -Id $creatorId -Name $agent.Name -Url $agent.Url
        }
        if ($component.VersionFrom -eq "minimumWindows") {
            $version = $DriverInfo.MinimumWindowsVersion
            $purl = $null
        }
        else {
            $version = $Toolchain.($component.VersionFrom).Version
            $purl = $Toolchain.($component.VersionFrom).Purl
        }
        if ($version -notmatch $VersionPattern) {
            Add-Problem "unknown version of '$($component.Name)'"
            $version = "NOASSERTION"
        }
        $ExternalIds[$Key] = "$Base#$Key"
        $package = @{
            Id      = $ExternalIds[$Key]
            Name    = $component.Name
            Version = $version
            Creator = $creatorId
            Purpose = $component.Purpose
            License = $component.License
            Purl    = $purl
        }
        Add-Package @package
        Add-Relationship -From $ExternalIds[$Key] -Type dependsOn -To $NoAssertionElement -Completeness noAssertion
    }
    return $ExternalIds[$Key]
}

#endregion

#region 3. Main

if (!$Env:SBOM_NAMESPACE) {
    throw "SBOM_NAMESPACE must be set in branding"
}
if (!$Env:PRODUCT_URL) {
    throw "PRODUCT_URL must be set in branding"
}

$Declaration = [System.IO.File]::ReadAllText($ComponentsFile) | ConvertFrom-Json
$DriverInfo = $Declaration.drivers.$Driver
if (!$DriverInfo) {
    throw "${ComponentsFile}: no entry for driver '$Driver'"
}

$PackagePath = (Resolve-Path -LiteralPath $PackagePath).ProviderPath
$Package = Get-PackageInfo
$Toolchain = Get-Toolchain
$SourceLocator = Get-SourceLocator

$Base = "$($Env:SBOM_NAMESPACE.TrimEnd('/'))/$Driver/$($Package.Version)"
$SbomId = "$Base/$Driver.spdx.json"
$VendorId = "$Base#vendor"

Add-Organization -Id $VendorId -Name $Env:VENDOR_NAME -Url $Env:PRODUCT_URL
Add-Element ([ordered]@{
        type   = "Tool"
        spdxId = "$Base#tool"
        name   = "New-DriverSbom $(Get-PackageVersion Product)"
    })

# The component: the driver package.
$primary = @{
    Id      = $Base
    Name    = $Driver
    Version = $Package.Version
    Creator = $VendorId
    Purpose = "deviceDriver"
    License = $DriverInfo.License
}
Add-Package @primary

# Contained: the files of the package, and the kits statically linked into its binaries, as declared for each kind
# of binary. Resource-only images link nothing.
$contained = @()
$kits = @()
foreach ($name in $Package.Files) {
    $path = Join-Path $PackagePath $name
    $fileId = "$Base/$([uri]::EscapeDataString($name))"
    $executable = $name -match '\.(sys|dll|exe)$'
    Add-File -Id $fileId -Path $path -Executable:$executable
    $contained += $fileId
    if ($executable) {
        $binary = Get-BinaryInfo $path
        if ($binary.HasCode) {
            $kits += $Declaration.staticLinkage.($binary.Kind)
        }
    }
}
foreach ($key in $kits | Select-Object -Unique) {
    $contained += Get-ExternalComponentId $key
}
Add-Relationship -From $Base -Type contains -To $contained

# Dependencies: Windows, and other driver packages (identified components outside of the scope of delivery, at their
# branded version).
$dependencies = @(Get-ExternalComponentId windows)
foreach ($name in $DriverInfo.DependsOn) {
    $dependency = @{
        Id      = "$Base#driver-$name"
        Name    = $name
        Version = (Get-PackageVersion -Raw $name).ToString()
        Creator = $VendorId
        Purpose = "deviceDriver"
    }
    Add-Package @dependency
    $dependencies += $dependency.Id
}
Add-Relationship -From $Base -Type dependsOn -To $dependencies

# Source code URI (BSI TR-03183-2 table 11). BSI uses software_SoftwareArtifact, which is abstract in SPDX 3.0.1;
# a software_Package with the "source" purpose is the closest concrete class.
if ($SourceLocator) {
    Add-Element ([ordered]@{
            type                    = "software_Package"
            spdxId                  = "$Base#source"
            name                    = "$Driver source"
            software_primaryPurpose = "source"
            externalRef             = @([ordered]@{
                    type            = "ExternalRef"
                    externalRefType = "sourceArtifact"
                    locator         = @($SourceLocator)
                })
        })
    Add-Relationship -From "$Base#source" -Type generates -To $Base
}

if ($Problems.Count) {
    throw "Strict SBOM generation failed for ${Driver}:`n - $($Problems -join "`n - ")"
}

#endregion

#region 4. Output

$Document = [ordered]@{
    "@context" = "https://spdx.org/rdf/3.0.1/spdx-context.jsonld"
    "@graph"   = @(
        [ordered]@{
            type         = "CreationInfo"
            "@id"        = "_:creationinfo"
            specVersion  = "3.0.1"
            created      = [datetime]::UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [cultureinfo]::InvariantCulture)
            createdBy    = @($VendorId)
            createdUsing = @("$Base#tool")
        },
        [ordered]@{
            type               = "SpdxDocument"
            spdxId             = "$SbomId#document"
            creationInfo       = "_:creationinfo"
            name               = "SBOM for $Driver $($Package.Version)"
            profileConformance = @("core", "software", "simpleLicensing")
            rootElement        = @($SbomId)
            element            = @($SbomId) + @($Graph | ForEach-Object { $_.spdxId })
        },
        [ordered]@{
            type              = "software_Sbom"
            spdxId            = $SbomId
            creationInfo      = "_:creationinfo"
            software_sbomType = @("build")
            rootElement       = @($Base)
        }
    ) + $Graph.ToArray()
}

New-Item -ItemType Directory -Path (Split-Path -Parent ([System.IO.Path]::GetFullPath($OutFile))) -Force | Out-Null
$Json = ($Document | ConvertTo-Json -Depth 16) -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($OutFile, "$Json`n", (New-Object System.Text.UTF8Encoding $false))

#endregion
