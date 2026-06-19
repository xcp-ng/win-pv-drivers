# Prepare branding info from provided environment variables.
# This script should be attestation-safe. Try our best to sanitize the environment.
# Use a highly-constrained parameter range.

#Requires -Version 7

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'

function Out-SafeString {
    [CmdletBinding()]
    param (
        [Parameter()]
        [string]$InputObject,
        [Parameter(Mandatory)]
        [ValidateSet(
            "Version",
            "VendorPrefix",
            "PathSafe",
            "Freeform",
            "Hex",
            "Guid",
            "Base64",
            "NumericBoolean",
            "DriverVer",
            "Uri"
        )]
        [string]$PatternType
    )

    Write-Verbose "InputObject '$InputObject'"
    Write-Verbose "PatternType '$PatternType'"

    # Allow basic punctuations only. All strings must be single-quote safe.
    $AllowedPatterns = @{
        "Version"        = '^([0-9]+(\.[0-9]+){0,3})?$'
        "VendorPrefix"   = '^([a-z][a-z0-9])?$'
        "PathSafe"       = '^[a-z0-9.,-_ ]*$'
        "Freeform"       = '^[a-z0-9().,-_ ]*$'
        "Hex"            = '^[a-f0-9]*$'
        "Guid"           = '^(\{[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\})?$'
        "Base64"         = '^[a-z0-9+/=\r\n\t ]*$'
        "NumericBoolean" = '^[01]?$'
        "DriverVer"      = '^([0-9]{2}/[0-9]{2}/[0-9]{4},[0-9]+(\.[0-9]+){0,3})?$'
        "Uri"            = '^[a-z0-9\-._~:/?#=@&+%]?$'
    }

    if ($InputObject -isnot [string]) {
        throw "Invalid input"
    }
    if (!$PatternType) {
        throw "Invalid pattern type $PatternType"
    }
    if ($PatternType -eq "Uri") {
        $uri = $null;
        if (![uri]::TryCreate($InputObject, [System.UriKind]::Absolute, [ref]$uri)) {
            throw "Invalid input for pattern type $PatternType"
        }
        if ($uri.Scheme -notin @("http", "https")) {
            throw "Invalid URI scheme $($uri.Scheme)"
        }
        return $uri.ToString();
    }

    $Pattern = $AllowedPatterns[$PatternType]
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

`$Env:FORCE_ACTIVATE = '$(Out-SafeString -PatternType NumericBoolean -InputObject $Env:FORCE_ACTIVATE)'
`$Env:FORCE_UNPLUG = '$(Out-SafeString -PatternType NumericBoolean -InputObject $Env:FORCE_UNPLUG)'

`$PackageVersions = @{
    Product         = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_Product)'
    xenbus          = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenbus)'
    xencons         = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xencons)'
    xenhid          = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenhid)'
    xeniface        = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xeniface)'
    xennet          = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xennet)'
    xenvbd          = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenvbd)'
    xenvif          = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenvif)'
    xenvkbd         = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenvkbd)'
    XenClean        = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_XenClean)'
    XenBootFix      = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_XenBootFix)'
    xenplus         = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xenplus)'
    XenTimeProvider = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_XenTimeProvider)'
    xstdvga         = '$(Out-SafeString -PatternType Version -InputObject $Env:PackageVersions_xstdvga)'
}

`$Env:MSI_UPGRADE_CODE_X86 = '$(Out-SafeString -PatternType Guid -InputObject $Env:MSI_UPGRADE_CODE_X86)'
`$Env:MSI_UPGRADE_CODE_X64 = '$(Out-SafeString -PatternType Guid -InputObject $Env:MSI_UPGRADE_CODE_X64)'
"@
Set-Content -Path $OutFile -Value $content -Force
