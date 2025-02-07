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

Update-ScriptFileInfo `
    "$ProjectDir\bin\$Platform\$Configuration\*\Invoke-XenClean.ps1" `
    -Description "XenClean invocation script" `
    -Version "$(Get-PackageVersion XenClean)" `
    -Author $Env:VENDOR_NAME `
    -CompanyName $Env:VENDOR_NAME `
    -Copyright $Env:COPYRIGHT `
    -Force

Set-SignerFileSignature -FilePath `
    "$ProjectDir\bin\$Platform\$Configuration\*\XenClean.exe", `
    "$ProjectDir\bin\$Platform\$Configuration\*\Invoke-XenClean.ps1"
