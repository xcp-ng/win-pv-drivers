[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\branding-generic.ps1"

$BrandingFile = "$ProjectDir\Branding.cs"
$OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
$NewBranding = `
@"
using System.Reflection;
[assembly:AssemblyVersion("${Env:MAJOR_VERSION}.${Env:MINOR_VERSION}.${Env:MICRO_VERSION}.${Env:BUILD_NUMBER}")]
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

        public const string MajorVersion = "${Env:MAJOR_VERSION}";
        public const string MinorVersion = "${Env:MINOR_VERSION}";
        public const string MicroVersion = "${Env:MICRO_VERSION}";
        public const string BuildNumber = "${Env:BUILD_NUMBER}";
    }
}
"@

if ($NewBranding -ne $OldBranding) {
    Write-Output "Updating Branding.cs"
    [System.IO.File]::WriteAllText($BrandingFile, $NewBranding)
}
