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

Import-Certificate -FilePath $PSScriptRoot\XCP-ng_Test_Signer.crt -CertStoreLocation Cert:\LocalMachine\Root
Import-Certificate -FilePath $PSScriptRoot\XCP-ng_Test_Signer.crt -CertStoreLocation Cert:\LocalMachine\TrustedPublisher

if (!$NoReboot) {
    Restart-Computer -Force
}
