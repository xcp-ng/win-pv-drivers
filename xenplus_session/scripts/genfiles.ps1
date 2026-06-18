[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\scripts\branding-generic.ps1"

$ver = Get-PackageVersion xenplus
$productVer = Get-PackageVersion Product
$description = "Xen session agent"

if ($true) {
    $BrandingFile = "$ProjectDir\Branding.inc"
    $OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
    $NewBranding = @"
// VS_VERSION_INFO=1, use that ID as VERSIONINFO or else
1 VERSIONINFO
 FILEVERSION $($ver.ToString().Replace(".", ","))
 PRODUCTVERSION $($productVer.ToString().Replace(".", ","))
 FILEFLAGSMASK 0x3fL
#ifdef _DEBUG
 FILEFLAGS 0x1L
#else
 FILEFLAGS 0x0L
#endif
 FILEOS 0x40004L
 FILETYPE 0x1L
 FILESUBTYPE 0x0L
BEGIN
    BLOCK "StringFileInfo"
    BEGIN
        BLOCK "200004b0"
        BEGIN
            VALUE "CompanyName", "${Env:VENDOR_NAME}"
            VALUE "FileDescription", "$description"
            VALUE "FileVersion", "$ver"
            VALUE "InternalName", "xenplus_session.exe"
            VALUE "LegalCopyright", "${Env:COPYRIGHT}"
            VALUE "OriginalFilename", "xenplus_session.exe"
            VALUE "ProductName", "${Env:PRODUCT_NAME}"
            VALUE "ProductVersion", "$productVer"
        END
    END
    BLOCK "VarFileInfo"
    BEGIN
        VALUE "Translation", 0x2000, 1200
    END
END

"@

    if ($NewBranding -ne $OldBranding) {
        Write-Output "Updating Branding.inc"
        Set-Content -Path $BrandingFile -Value $NewBranding -NoNewline
    }
}

if ($true) {
    $BrandingFile = "$ProjectDir\Branding.cs"
    $OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
    $NewBranding = @"
using System.Reflection;
[assembly:AssemblyVersion("$ver")]
[assembly:AssemblyInformationalVersion("$productVer")]
[assembly:AssemblyCompany("${Env:VENDOR_NAME}")]
[assembly:AssemblyProduct("${Env:PRODUCT_NAME}")]
[assembly:AssemblyCopyright("${Env:COPYRIGHT}")]
[assembly:AssemblyTitle("$description")]

namespace XenPlus {
    static class VersionInfo {
        public const string FileVersion = "$ver";
        public const string ProductVersion = "$productVer";
        public const string Description = "$description";
        public const string VendorName = "${Env:VENDOR_NAME}";
        public const string ProductName = "${Env:PRODUCT_NAME}";
        public const string Copyright = "${Env:COPYRIGHT}";
    }
}
"@

    if ($NewBranding -ne $OldBranding) {
        Write-Output "Updating Branding.cs"
        Set-Content -Path $BrandingFile -Value $NewBranding -NoNewline
    }
}
