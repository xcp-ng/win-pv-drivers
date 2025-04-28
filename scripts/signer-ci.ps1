# Prepare signer info from provided environment variables.

#Requires -Version 7

[CmdletBinding()]
param (
    [Parameter()]
    [string]$OutFile = "$PSScriptRoot\..\branding.ps1"
)

$ErrorActionPreference = 'Stop'

$pfxBytes = [System.Convert]::FromBase64String($Env:SIGNER_PFX_BASE64)
$pfxPath = Join-Path $pwd "Signer.pfx"
[IO.File]::WriteAllBytes($pfxPath, $pfxBytes)
try {
    certutil -importpfx -f -user -p "" my $pfxPath nochain
    if ($LASTEXITCODE -ne 0) {
        throw "certutil failed with error $LASTEXITCODE"
    }
    $signer = (Get-PfxCertificate -FilePath $pfxPath -NoPromptForPassword).Thumbprint
}
finally {
    Remove-Item -Force $pfxPath
}
Add-Content -Path $OutFile -Value "`$Env:SIGNER = '$signer'" -Force
