[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\scripts\branding-generic.ps1"

$BrandingFile = "$ProjectDir\Branding.inc"
$OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore
$NewBranding = @"
// VS_VERSION_INFO=1, use that ID as VERSIONINFO or else
1 VERSIONINFO
 FILEVERSION $((Get-PackageVersion xenplus).ToString().Replace(".", ","))
 PRODUCTVERSION $((Get-PackageVersion Product).ToString().Replace(".", ","))
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
            VALUE "FileDescription", "Xen session agent"
            VALUE "FileVersion", "$(Get-PackageVersion xenplus)"
            VALUE "InternalName", "xenplus_session.exe"
            VALUE "LegalCopyright", "${Env:COPYRIGHT}"
            VALUE "OriginalFilename", "xenplus_session.exe"
            VALUE "ProductName", "${Env:PRODUCT_NAME}"
            VALUE "ProductVersion", "$(Get-PackageVersion Product)"
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
