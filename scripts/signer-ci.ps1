# Prepare signer info from provided environment variables.

#Requires -Version 7

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'

if (!$Env:SIGNER_PFX_BASE64) {
    exit 0
}
$pfxBytes = [System.Convert]::FromBase64String([regex]::Replace($Env:SIGNER_PFX_BASE64, '[^a-zA-Z0-9+/=]', ''))
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
Add-Content -Path $Env:GITHUB_STEP_SUMMARY -Value "Signer thumbprint: ``$signer``" -Force
