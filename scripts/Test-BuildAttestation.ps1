#Requires -Version 7

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$Bundle,
    [Parameter(Mandatory)]
    [string]$Path,
    [Parameter(Mandatory)]
    [string]$Repository,
    [Parameter()]
    [string]$Filter,
    [Parameter()]
    [string[]]$Include,
    [Parameter()]
    [string[]]$Exclude
)

$ErrorActionPreference = 'Stop'

$bundlePath = Convert-Path $Bundle

# cache the trusted root for faster verification
$root = New-TemporaryFile
gh attestation trusted-root | Set-Content -Path $root -Encoding UTF8
if ($LASTEXITCODE -ne 0) {
    throw "Cannot fetch trusted root"
}
Write-Information "Trusted root cached to $root"

try {
    Push-Location $Path | Out-Null
    return (Get-ChildItem -Recurse -File -Filter $Filter -Include $Include -Exclude $Exclude |
        Resolve-Path -Relative |
        ForEach-Object {
            gh attestation verify $_ `
                --custom-trusted-root $root.FullName `
                --bundle $bundlePath `
                --deny-self-hosted-runners `
                --repo $Repository > $null
            if ($LASTEXITCODE -ne 0) {
                throw "Attestation failed for $_"
            }
            Write-Information "Attestation succeeded for $_"
        })
}
finally {
    Pop-Location | Out-Null
    Remove-Item $root -Force
}
