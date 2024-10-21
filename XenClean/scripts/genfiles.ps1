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
[assembly:AssemblyTitle("Xen PV driver cleaning tool")]

namespace XenClean {
    internal static class VersionInfo {
        public const string MsiUpgradeCodeX86 = "${Env:MSI_UPGRADE_CODE_X86}";
        public const string MsiUpgradeCodeX64 = "${Env:MSI_UPGRADE_CODE_X64}";
    }
}
"@

if ($NewBranding -ne $OldBranding) {
    Write-Output "Updating Branding.cs"
    [System.IO.File]::WriteAllText($BrandingFile, $NewBranding)
}
