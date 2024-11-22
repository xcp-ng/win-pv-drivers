[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir,
    [Parameter(Mandatory)]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [string]$Platform
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\branding-generic.ps1"
. "$ProjectDir\..\scripts\sign.ps1"

if (![string]::IsNullOrEmpty($Env:SIGNER_THUMBPRINT)) {
    SignFile `
        -SigningCertificateThumbprint $Env:SIGNER_THUMBPRINT `
        -FilePath "$ProjectDir\$Platform\$Configuration\XenBootFix.exe"
}
