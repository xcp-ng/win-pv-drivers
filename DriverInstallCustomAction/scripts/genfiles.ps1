[CmdletBinding()]
param (
    [Parameter()]
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
[assembly:AssemblyTitle("Xen PV driver installation library")]
"@

if ($NewBranding -ne $OldBranding) {
    Write-Output "Updating Branding.cs"
    [System.IO.File]::WriteAllText($BrandingFile, $NewBranding)
}
