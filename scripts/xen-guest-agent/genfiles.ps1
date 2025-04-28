[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\scripts\branding-generic.ps1"

function Update-BrandingFile {
    $BrandingFile = "$ProjectDir\xen-guest-agent\branding.rs"
    $OldBranding = Get-Content -Raw $BrandingFile -ErrorAction Ignore

    $FileVersion = Get-PackageVersion XenGuestAgent
    $NewFileVersionValue = [string]::Format("0x{0:x}", `
        ([uint64]$FileVersion.Major -shl 48) -bor `
        ([uint64]$FileVersion.Minor -shl 32) -bor `
        ([uint64]$FileVersion.Build -shl 16) -bor `
            [uint64]$FileVersion.Revision)

    $ProductVersion = Get-PackageVersion Product
    $NewProductVersionValue = [string]::Format("0x{0:x}", `
        ([uint64]$ProductVersion.Major -shl 48) -bor `
        ([uint64]$ProductVersion.Minor -shl 32) -bor `
        ([uint64]$ProductVersion.Build -shl 16) -bor `
            [uint64]$ProductVersion.Revision)

    $NewBranding = @"
loop {
    res.set_version_info(winres::VersionInfo::FILEVERSION, ${NewFileVersionValue});
    res.set_version_info(winres::VersionInfo::PRODUCTVERSION, ${NewProductVersionValue});

    let crate_version = env!("CARGO_PKG_VERSION");
    let file_description = format!("Xen Guest Agent (v{crate_version}, Rust-based)");

    res.set("CompanyName", "${Env:VENDOR_NAME}");
    res.set("FileDescription", &file_description);
    res.set("FileVersion", "$($FileVersion.ToString())");
    res.set("InternalName", "xen-guest-agent.exe");
    res.set("LegalCopyright", "${Env:COPYRIGHT}");
    res.set("OriginalFilename", "xen-guest-agent.exe");
    res.set("ProductName", "${Env:PRODUCT_NAME}");
    res.set("ProductVersion", "$($ProductVersion.ToString())");

    break;
}
"@

    if ($NewBranding -ne $OldBranding) {
        Write-Output "Updating branding.rs"
        Set-Content -Path $BrandingFile -Value $NewBranding -NoNewline
    }
}

function Update-AgentVersionFile {
    $VersionFile = "$ProjectDir\publishers\publisher-xenstore\src\version.rs"
    $OldVersion = Get-Content -Raw $VersionFile -ErrorAction Ignore

    $Version = Get-PackageVersion XenGuestAgent

    $NewVersion = @"
pub(crate) const AGENT_VERSION_MAJOR: &str = "$($Version.Major)"; // XO does not show version at all if 0
pub(crate) const AGENT_VERSION_MINOR: &str = "$($Version.Minor)";
pub(crate) const AGENT_VERSION_MICRO: &str = "$($Version.Build)"; // XAPI exposes "-1" if missing
pub(crate) const AGENT_VERSION_BUILD: &str = "$($Version.Revision)";
"@

    if ($NewVersion -ne $OldVersion) {
        Write-Output "Updating $VersionFile"
        Set-Content -Path $VersionFile -Value $NewVersion -NoNewline
    }
}

Update-BrandingFile
Update-AgentVersionFile
