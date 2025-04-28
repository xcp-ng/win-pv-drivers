function Get-PackageVersion {
    [CmdletBinding()]
    param (
        [Parameter(Position = 1)]
        [string]$PackageName = 'Product'
    )
    $Version = $PackageVersions[$PackageName]
    if (!$Version) {
        throw "Cannot get $PackageName version"
    }
    return [version]::Parse($Version)
}

if (!$Env:VENDOR_NAME) {
    $Env:VENDOR_NAME = 'Xen Project'
}

if (!$Env:VENDOR_PREFIX) {
    $Env:VENDOR_PREFIX = 'XP'
}

if (!$Env:PRODUCT_NAME) {
    $Env:PRODUCT_NAME = 'Xen'
}

# xeniface
if (!$Env:OBJECT_PREFIX) {
    $Env:OBJECT_PREFIX = 'XenProject'
}

if (!$Env:COPYRIGHT) {
    $Env:COPYRIGHT = 'Copyright (c) Xen Project.'
}

if ($null -eq $PackageVersions) {
    $PackageVersions = @{
    }
}
if ($null -eq $PackageVersions['Product']) {
    $PackageVersions['Product'] = '9.1.0'
}
if ($null -eq $PackageVersions['xenbus']) {
    $PackageVersions['xenbus'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['xencons']) {
    $PackageVersions['xencons'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['xenhid']) {
    $PackageVersions['xenhid'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['xeniface']) {
    $PackageVersions['xeniface'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['xennet']) {
    $PackageVersions['xennet'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['xenvbd']) {
    $PackageVersions['xenvbd'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['xenvif']) {
    $PackageVersions['xenvif'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['xenvkbd']) {
    $PackageVersions['xenvkbd'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['XenClean']) {
    $PackageVersions['XenClean'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['XenBootFix']) {
    $PackageVersions['XenBootFix'] = $PackageVersions['Product']
}
if ($null -eq $PackageVersions['XenGuestAgent']) {
    $PackageVersions['XenGuestAgent'] = $PackageVersions['Product']
}

if (!$Env:MSI_UPGRADE_CODE_X86) {
    $Env:MSI_UPGRADE_CODE_X86 = '{10828840-D8A9-4953-B44A-1F1D3CD7ECB0}'
}

if (!$Env:MSI_UPGRADE_CODE_X64) {
    $Env:MSI_UPGRADE_CODE_X64 = '{D60FED1E-316C-41B0-B7A5-E44951A82618}'
}
