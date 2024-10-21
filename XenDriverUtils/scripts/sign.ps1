[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ProjectDir,
    [Parameter(Mandatory)]
    [string]$Configuration,
    [Parameter()]
    [string]$Platform
)

. "$ProjectDir\..\branding.ps1"
. "$ProjectDir\..\branding-generic.ps1"
. "$ProjectDir\..\scripts\sign.ps1"

if ($null -ne $Script:SigningCertificate) {
    SignFile -FilePath "$ProjectDir\bin\$Platform\$Configuration\*\xdutils.dll"
}
