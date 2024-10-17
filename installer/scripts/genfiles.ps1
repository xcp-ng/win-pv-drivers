[CmdletBinding()]
param (
    [Parameter()]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\branding-generic.ps1"

$BrandingFile = "$ProjectDir\Branding.wxi"
$OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
$NewBranding = `
@"
<?xml version="1.0" encoding="utf-8"?>
<Include xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <?define VENDOR_NAME="${Env:VENDOR_NAME}"?>
  <?define PRODUCT_NAME="${Env:PRODUCT_NAME}"?>
  <?define VENDOR_PREFIX="${Env:VENDOR_PREFIX}"?>
  <?define VENDOR_DEVICE_ID="${Env:VENDOR_DEVICE_ID}"?>
  <?define COPYRIGHT="${Env:COPYRIGHT}"?>

  <?define MAJOR_VERSION="${Env:MAJOR_VERSION}"?>
  <?define MINOR_VERSION="${Env:MINOR_VERSION}"?>
  <?define MICRO_VERSION="${Env:MICRO_VERSION}"?>
  <?define BUILD_NUMBER="${Env:BUILD_NUMBER}"?>

  <?define MSI_UPGRADE_CODE_X86="${Env:MSI_UPGRADE_CODE_X86}"?>
  <?define MSI_UPGRADE_CODE_X64="${Env:MSI_UPGRADE_CODE_X64}"?>
</Include>
"@

if ($NewBranding -ne $OldBranding) {
    Write-Output "Updating Branding.wxi"
    [System.IO.File]::WriteAllText($BrandingFile, $NewBranding)
}
