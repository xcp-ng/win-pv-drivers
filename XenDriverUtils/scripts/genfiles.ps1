[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\scripts\branding-generic.ps1"

$BrandingFile = "$ProjectDir\Branding.cs"
$OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
$NewBranding = `
    @"
using System.Reflection;
[assembly:AssemblyVersion("$(Get-PackageVersion Product)")]
[assembly:AssemblyCompany("${Env:VENDOR_NAME}")]
[assembly:AssemblyProduct("${Env:PRODUCT_NAME}")]
[assembly:AssemblyCopyright("${Env:COPYRIGHT}")]
[assembly:AssemblyTitle("Xen PV driver utility library")]

namespace XenDriverUtils {
    internal static class VersionInfo {
        public const string VendorName = "${Env:VENDOR_NAME}";
        public const string ProductName = "${Env:PRODUCT_NAME}";
        public const string VendorPrefix = "${Env:VENDOR_PREFIX}";
        public const string VendorDeviceId = "${Env:VENDOR_DEVICE_ID}";
        public const string Copyright = "${Env:COPYRIGHT}";
    }
}
"@

if ($NewBranding -ne $OldBranding) {
    Write-Output "Updating Branding.cs"
    Set-Content -Path $BrandingFile -Value $NewBranding -NoNewline
}
