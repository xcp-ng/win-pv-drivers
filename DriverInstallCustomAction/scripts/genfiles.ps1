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

$BrandingFile = "$ProjectDir\Branding.cs"
$NewBranding = `
@"
namespace XenInstCA {
    internal static class Version {
        public const string VendorName = "$Env:VENDOR_NAME";
        public const string ProductName = "$Env:PRODUCT_NAME";
        public const string VendorPrefix = "$Env:VENDOR_PREFIX";
        public const string VendorDeviceId = "$Env:VENDOR_DEVICE_ID";
        public const string Copyright = "$Env:COPYRIGHT";

        public const string MajorVersion = "$Env:MAJOR_VERSION";
        public const string MinorVersion = "$Env:MINOR_VERSION";
        public const string MicroVersion = "$Env:MICRO_VERSION";
        public const string BuildNumber = "$Env:BUILD_NUMBER";
    }
}
"@

Write-Output "Updating Branding.cs"
[System.IO.File]::WriteAllText($BrandingFile, $NewBranding)
