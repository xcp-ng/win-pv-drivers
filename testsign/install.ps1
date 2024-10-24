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

bcdedit.exe -set testsigning on
if ($LASTEXITCODE -ne 0) {
    throw "Cannot enable testsigning; is Secure Boot turned off?"
}

certutil -addstore -f Root $PSScriptRoot\XCP-ng_Test_Signer.crt
certutil -addstore -f TrustedPublisher $PSScriptRoot\XCP-ng_Test_Signer.crt

if (!$NoReboot) {
    Restart-Computer -Force
}
