[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\scripts\branding-generic.ps1"
. "$ProjectDir\..\scripts\sign.ps1"

$ver = Get-PackageVersion xenplus
$productVer = Get-PackageVersion xenplus

$BrandingFile = "$ProjectDir\Branding.cs"
$OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
$NewBranding = `
    @"
using System.Reflection;
[assembly:AssemblyVersion("$ver")]
[assembly:AssemblyInformationalVersion("$productVer")]
[assembly:AssemblyCompany("${Env:VENDOR_NAME}")]
[assembly:AssemblyProduct("${Env:PRODUCT_NAME}")]
[assembly:AssemblyCopyright("${Env:COPYRIGHT}")]

namespace XenPlus {
    static class VersionInfo {
        public const string ProductVersion = "$productVer";
    }
}
"@

if ($NewBranding -ne $OldBranding) {
    Write-Output "Updating Branding.cs"
    Set-Content -Path $BrandingFile -Value $NewBranding -NoNewline
}
