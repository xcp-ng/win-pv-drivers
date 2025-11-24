[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\scripts\branding-generic.ps1"

$BrandingFile = "$ProjectDir\Branding.wxi"
$OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
$NewBranding = @"
<?xml version="1.0" encoding="utf-8"?>
<Include xmlns="http://wixtoolset.org/schemas/v4/wxs">
    <?define VENDOR_NAME="${Env:VENDOR_NAME}"?>
    <?define PRODUCT_NAME="${Env:PRODUCT_NAME}"?>
    <?define VENDOR_PREFIX="${Env:VENDOR_PREFIX}"?>
    <?define VENDOR_DEVICE_ID="${Env:VENDOR_DEVICE_ID}"?>
    <?define COPYRIGHT="${Env:COPYRIGHT}"?>
"@

foreach ($package in @("Product", "Xenbus", "Xencons", "Xenhid", "Xeniface", "Xennet", "Xenvbd", "Xenvif", "Xenvkbd", "XenClean", "XenBootFix", "XenGuestAgent", "XenTimeProvider")) {
    $NewBranding += @"

    <?define ${package}Version="$(Get-PackageVersion $package)"?>
"@
}

$NewBranding += @"

    <?define MSI_UPGRADE_CODE_X86="${Env:MSI_UPGRADE_CODE_X86}"?>
    <?define MSI_UPGRADE_CODE_X64="${Env:MSI_UPGRADE_CODE_X64}"?>
</Include>
"@

if ($NewBranding -ne $OldBranding) {
    Write-Output "Updating Branding.wxi"
    Set-Content -Path $BrandingFile -Value $NewBranding -NoNewline
}
