function Get-PackageVersion {
    [CmdletBinding()]
    param (
        [Parameter(Position = 1)]
        [string]$PackageName = 'Product',
        # Don't fill in 0 for non-existent build/revision numbers.
        [Parameter()]
        [switch]$Raw
    )
    $VersionString = $PackageVersions[$PackageName]
    if (!$VersionString) {
        throw "Cannot get $PackageName version"
    }
    $RawVersion = [version]::Parse($VersionString)
    if ($Raw) {
        return $RawVersion
    }
    else {
        $build = if ($RawVersion.Build -eq -1) { 0 } else { $RawVersion.Build }
        $rev = if ($RawVersion.Revision -eq -1) { 0 } else { $RawVersion.Revision }
        return [version]::new($RawVersion.Major, $RawVersion.Minor, $build, $rev)
    }
}

if (!$Env:VENDOR_KEY) {
    throw 'VENDOR_KEY must be set in branding.ps1.'
}
if (!$Env:VENDOR_PREFIX) {
    throw 'VENDOR_PREFIX must be set in branding.ps1.'
}
if (!$PackageVersions -or !$PackageVersions['Product']) {
    throw 'PackageVersions.Product must be set in branding.ps1.'
}
if (!$Env:MSI_UPGRADE_CODE_X86) {
    throw 'MSI_UPGRADE_CODE_X86 must be set in branding.ps1.'
}
if (!$Env:MSI_UPGRADE_CODE_X64) {
    throw 'MSI_UPGRADE_CODE_X64 must be set in branding.ps1.'
}

if (!$Env:VENDOR_NAME) {
    $Env:VENDOR_NAME = $Env:VENDOR_KEY
}

if (!$Env:PRODUCT_NAME) {
    $Env:PRODUCT_NAME = 'Xen'
}

# xeniface
if (!$Env:OBJECT_PREFIX) {
    $Env:OBJECT_PREFIX = 'XenProject'
}

if (!$Env:COPYRIGHT) {
    $Env:COPYRIGHT = "Copyright (c) $($Env:VENDOR_NAME), Xen Project, and others"
}
if (!$PackageVersions['xenbus']) {
    $PackageVersions['xenbus'] = $PackageVersions['Product']
}
if (!$PackageVersions['xencons']) {
    $PackageVersions['xencons'] = $PackageVersions['Product']
}
if (!$PackageVersions['xenhid']) {
    $PackageVersions['xenhid'] = $PackageVersions['Product']
}
if (!$PackageVersions['xeniface']) {
    $PackageVersions['xeniface'] = $PackageVersions['Product']
}
if (!$PackageVersions['xennet']) {
    $PackageVersions['xennet'] = $PackageVersions['Product']
}
if (!$PackageVersions['xenvbd']) {
    $PackageVersions['xenvbd'] = $PackageVersions['Product']
}
if (!$PackageVersions['xenvif']) {
    $PackageVersions['xenvif'] = $PackageVersions['Product']
}
if (!$PackageVersions['xenvkbd']) {
    $PackageVersions['xenvkbd'] = $PackageVersions['Product']
}
if (!$PackageVersions['XenClean']) {
    $PackageVersions['XenClean'] = $PackageVersions['Product']
}
if (!$PackageVersions['XenBootFix']) {
    $PackageVersions['XenBootFix'] = $PackageVersions['Product']
}
if (!$PackageVersions['xenplus']) {
    $PackageVersions['xenplus'] = $PackageVersions['Product']
}
