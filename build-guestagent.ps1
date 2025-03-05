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
    cargo.exe remove --package publisher-xenstore --target --% "cfg(target_os = ""windows"")" xenstore-win
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }
    cargo.exe remove --package vif-detect --target --% "cfg(target_os = ""windows"")" xenstore-win
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    cargo.exe add --package publisher-xenstore --target --% "cfg(target_os = ""windows"")" --path ..\xenstore-win
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }
    cargo.exe add --package vif-detect --target --% "cfg(target_os = ""windows"")" --path ..\xenstore-win
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    Write-Host "Installing winres"
    cargo.exe add --build --package xen-guest-agent --target "cfg(windows)" "winres@0.1"
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    Copy-Item -Force ..\scripts\xen-guest-agent\build.rs .\xen-guest-agent\build.rs
    Copy-Item -Force ..\scripts\xen-guest-agent\manifest.xml .\xen-guest-agent\manifest.xml
    New-Item -Type Directory .\.cargo -Force
    Copy-Item -Force ..\scripts\xen-guest-agent\config.toml .\.cargo\config.toml
    ..\scripts\xen-guest-agent\genfiles.ps1 -ProjectDir .

    Write-Host "Cleaning"
    cargo.exe clean
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    $cargoArgs = @(
        "--no-default-features",
        "--locked",
        "-p",
        "xen-guest-agent"
    )

    if ($Configuration -ieq "release") {
        $cargoArgs += @("--release")
    }

    Write-Host "Building"
    cargo.exe build @cargoArgs
    if ($LASTEXITCODE -ne 0) {
        throw "cargo failed with error $LASTEXITCODE"
    }

    Write-Host "Signing"
    Set-SignerFileSignature .\target\$Configuration\xen-guest-agent.exe
}
finally {
    Pop-Location
}
