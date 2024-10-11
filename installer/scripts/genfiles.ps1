[CmdletBinding()]
param (
    [Parameter()]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"

if ([string]::IsNullOrEmpty($Env:VENDOR_NAME)) {
    Set-Item -Path Env:VENDOR_NAME -Value 'Xen Project'
}

if ([string]::IsNullOrEmpty($Env:VENDOR_PREFIX)) {
    Set-Item -Path Env:VENDOR_PREFIX -Value 'XP'
}

if ([string]::IsNullOrEmpty($Env:PRODUCT_NAME)) {
    Set-Item -Path Env:PRODUCT_NAME -Value 'Xen'
}

if ([string]::IsNullOrEmpty($Env:COPYRIGHT)) {
    Set-Item -Path Env:COPYRIGHT -Value 'Copyright (c) Xen Project.'
}

if ([string]::IsNullOrEmpty($Env:BUILD_NUMBER)) {
    if (Test-Path ".build_number") {
        $BuildNum = Get-Content -Path ".build_number"
        Set-Content -Path ".build_number" -Value ([int]$BuildNum + 1)
    } else {
        $BuildNum = '0'
        Set-Content -Path ".build_number" -Value '1'
    }
    Set-Item -Path Env:BUILD_NUMBER -Value $BuildNum
}

if ([string]::IsNullOrEmpty($Env:MAJOR_VERSION)) {
    Set-Item -Path Env:MAJOR_VERSION -Value '9'
}

if ([string]::IsNullOrEmpty($Env:MINOR_VERSION)) {
    Set-Item -Path Env:MINOR_VERSION -Value '1'
}

if ([string]::IsNullOrEmpty($Env:MICRO_VERSION)) {
    Set-Item -Path Env:MICRO_VERSION -Value '0'
}

if ([string]::IsNullOrEmpty($Env:MSI_UPGRADE_CODE_X86)) {
    Set-Item -Path Env:MSI_UPGRADE_CODE_X86 -Value '{10828840-D8A9-4953-B44A-1F1D3CD7ECB0}'
}

if ([string]::IsNullOrEmpty($Env:MSI_UPGRADE_CODE_X64)) {
    Set-Item -Path Env:MSI_UPGRADE_CODE_X64 -Value '{D60FED1E-316C-41B0-B7A5-E44951A82618}'
}

$BrandingFile = "$ProjectDir\Branding.wxi"
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

Write-Output "Updating Branding.wxi"
[System.IO.File]::WriteAllText($BrandingFile, $NewBranding)
