function SignFile() {
    param (
        [Parameter(Mandatory)]
        [string]$SigningCertificate,
        [Parameter()]
        [string[]]$FilePath
    )

    if ($SigningCertificate.EndsWith(".pfx", [System.StringComparison]::OrdinalIgnoreCase)) {
        $SignerObject = Get-PfxCertificate -FilePath $SigningCertificate
    }
    else {
        $SignerObject = Get-ChildItem Cert:\CurrentUser\My\$SigningCertificate
    }
    $signArgs = @{
        HashAlgorithm = "SHA256"
        Certificate   = $SignerObject
    }
    if (![string]::IsNullOrEmpty($Env:TimestampServer)) {
        $signArgs["TimestampServer"] = $Env:TimestampServer
    }

    Set-AuthenticodeSignature -FilePath $FilePath @signArgs
}

function ExportSignerCertificate {
    param (
        [Parameter(Mandatory)]
        [string]$SigningCertificate,
        [Parameter()]
        [string[]]$OutDir
    )

    if ($SigningCertificate.EndsWith(".pfx", [System.StringComparison]::OrdinalIgnoreCase)) {
        $SignerObject = Get-PfxCertificate -FilePath $SigningCertificate
    }
    else {
        $SignerObject = Get-ChildItem Cert:\CurrentUser\My\$SigningCertificate
    }

    Export-Certificate -Cert $SignerObject -FilePath $OutDir\$($SignerObject.Thumbprint).crt -Type CERT -Force
}
