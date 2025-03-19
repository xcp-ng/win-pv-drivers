[CmdletBinding()]
param (
    [Parameter(Mandatory, ParameterSetName = "build")]
    [ValidateSet("debug", "release")]
    [string]$Configuration
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

# PowerShell's quoting rules are unfortunately unstable between 5.1 and 7.x, thus the workaround
$PSNativeCommandArgumentPassing = "Legacy"

Push-Location $PSScriptRoot\xen-guest-agent
try {
    Write-Host "Reconfiguring xenstore-win"
    if (!(Get-Content -Encoding utf8 $PSScriptRoot\xen-guest-agent\Cargo.toml | Select-String -SimpleMatch "[patch.'https://github.com/TSnake41/xenstore-win.git']")) {
        Add-Content -Encoding utf8 -Path $PSScriptRoot\xen-guest-agent\Cargo.toml -Value @"

[patch.'https://github.com/TSnake41/xenstore-win.git']
xenstore-win = { path = "../xenstore-win" }

"@
    }

    Copy-Item -Force ..\scripts\xen-guest-agent\build.rs .\xen-guest-agent\build.rs
    & "$PSScriptRoot\scripts\xen-guest-agent\genfiles.ps1" -ProjectDir .

    Write-Host "Cleaning"
    cargo.exe clean
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    $cargoArgs = @(
        "--no-default-features",
        "--locked"
    )

    if ($Configuration -ieq "release") {
        $cargoArgs += @("--release")
    }

    Write-Host "Building xen-guest-agent"
    cargo.exe build @cargoArgs -p xen-guest-agent
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    Write-Host "Building xen-win-clipboard"
    cargo.exe build @cargoArgs -p xen-win-clipboard
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    Write-Host "Signing"
    Set-SignerFileSignature .\target\$Configuration\xen-guest-agent.exe, .\target\$Configuration\xen-win-clipboard.exe
}
finally {
    Pop-Location
}
