#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param (
    [Parameter()]
    [switch]$NoReboot
)

$ErrorActionPreference = "Stop"

if (!$PSCmdlet.ShouldProcess("Local computer", "Set up testsigned XCP-ng driver - reduces your security")) {
    exit;
}

$System32 = [System.Environment]::SystemDirectory

& "$System32\bcdedit.exe" -set testsigning on
if ($LASTEXITCODE -ne 0) {
    throw "Cannot enable testsigning; is Secure Boot turned off?"
}

Get-ChildItem $PSScriptRoot\*.crt | ForEach-Object {
    & "$System32\certutil.exe" -addstore -f Root $_
    & "$System32\certutil.exe" -addstore -f TrustedPublisher $_
}

if (!$NoReboot) {
    Restart-Computer -Force
}
