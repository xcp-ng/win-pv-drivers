# Prepare branding info from provided environment variables.
# This script should be attestation-safe. Try our best to sanitize the environment.
# Use a highly-constrained parameter range.

#Requires -Version 7

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$OutFile,
    [Parameter()]
    [switch]$AddSigner
)

$ErrorActionPreference = 'Stop'

function Out-SafeString {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [string]$InputObject,
        [Parameter(Mandatory)]
        [ValidateSet("Version", "VendorPrefix", "PathSafe", "Freeform", "Hex", "Guid", "Base64", "OneOrEmpty")]
        [string]$PatternType
    )

    Write-Verbose "InputObject '$InputObject'"
    Write-Verbose "PatternType '$PatternType'"

    # Allow basic punctuations only. All strings must be single-quote safe.
    $AllowedPatterns = @{
        "Version"      = '^[0-9]+(\.[0-9]+){0,3}$'
        "VendorPrefix" = '^[a-z][a-z0-9]{0,3}$'
        "PathSafe"     = '^[a-z0-9.,-_ ]*$'
        "Freeform"     = '^[a-z0-9().,-_ ]*$'
        "Hex"          = '^[a-f0-9]*$'
        "Guid"         = '^\{[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\}$'
        "Base64"       = '^[a-z0-9+/=\r\n\t ]*$'
        "OneOrEmpty"   = '^1?$'
    }

    if ($InputObject -isnot [string]) {
        throw "Invalid input"
    }
    $Pattern = $AllowedPatterns[$PatternType]
    if (!$PatternType) {
        throw "Invalid pattern type $PatternType"
    }

    if (!($InputObject -imatch $Pattern)) {
        throw "Invalid input for pattern type $PatternType"
    }

    return $InputObject
}

# Branding file is emitted as step summary. Don't put any secrets here!
$content = @"
`$Env:VENDOR_NAME = '$(Out-SafeString -PatternType PathSafe -InputObject $Env:VENDOR_NAME)'
`$Env:PRODUCT_NAME = '$(Out-SafeString -PatternType PathSafe -InputObject $Env:PRODUCT_NAME)'
`$Env:VENDOR_PREFIX = '$(Out-SafeString -PatternType VendorPrefix -InputObject $Env:VENDOR_PREFIX)'
`$Env:COPYRIGHT = '$(Out-SafeString -PatternType Freeform -InputObject $Env:COPYRIGHT)'

`$Env:FORCE_ACTIVATE = '$(Out-SafeString -PatternType OneOrEmpty -InputObject $Env:FORCE_ACTIVATE)'
`$Env:FORCE_UNPLUG = '$(Out-SafeString -PatternType OneOrEmpty -InputObject $Env:FORCE_UNPLUG)'

`$PackageVersions = @{
    Product       = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_Product)'
    xenbus        = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenbus)'
    xencons       = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xencons)'
    xenhid        = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenhid)'
    xeniface      = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xeniface)'
    xennet        = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xennet)'
    xenvbd        = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenvbd)'
    xenvif        = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenvif)'
    xenvkbd       = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenvkbd)'
    XenClean      = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_XenClean)'
    XenBootFix    = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_XenBootFix)'
    XenGuestAgent = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_XenGuestAgent)'
}

`$Env:MSI_UPGRADE_CODE_X86 = '$(Out-SafeString -PatternType Guid -InputObject $Env:MSI_UPGRADE_CODE_X86)'
`$Env:MSI_UPGRADE_CODE_X64 = '$(Out-SafeString -PatternType Guid -InputObject $Env:MSI_UPGRADE_CODE_X64)'
"@
Set-Content -Path $OutFile -Value $content -Force

if ($AddSigner) {
    # Doesn't hurt to verify again.
    Out-SafeString -PatternType Base64 -InputObject $Env:SIGNER_PFX_BASE64 | Out-Null
    & "$PSScriptRoot\signer-ci.ps1" -OutFile $OutFile
}

Add-Content -Path $Env:GITHUB_STEP_SUMMARY -Value "Branding:", "``````", $content, "``````" -Force
