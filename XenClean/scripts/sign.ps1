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
. "$ProjectDir\..\scripts\branding-generic.ps1"
. "$ProjectDir\..\scripts\sign.ps1"

Update-ScriptFileInfo `
    "$ProjectDir\bin\$Platform\$Configuration\*\Invoke-XenClean.ps1" `
    -Description "XenClean invocation script" `
    -Version "$(Get-PackageVersion XenClean)" `
    -Author $Env:VENDOR_NAME `
    -CompanyName $Env:VENDOR_NAME `
    -Copyright $Env:COPYRIGHT `
    -Force

# re Copy-XenVifSettings: Copied deps are copied from source (not output) and therefore need signing again.
Set-SignerFileSignature -FilePath `
    "$ProjectDir\bin\$Platform\$Configuration\*\XenClean.exe", `
    "$ProjectDir\bin\$Platform\$Configuration\*\Invoke-XenClean.ps1", `
    "$ProjectDir\bin\$Platform\$Configuration\*\Copy-XenVifSettings.ps1"
