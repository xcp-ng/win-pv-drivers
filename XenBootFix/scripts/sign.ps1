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

Set-SignerFileSignature -FilePath "$ProjectDir\$Platform\$Configuration\XenBootFix.exe"
