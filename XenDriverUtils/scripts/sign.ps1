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

if (![string]::IsNullOrEmpty($Env:SigningCertificateThumbprint)) {
    SignFile `
        -SigningCertificateThumbprint $Env:SigningCertificateThumbprint `
        -FilePath "$ProjectDir\bin\$Platform\$Configuration\*\xdutils.dll"
}
