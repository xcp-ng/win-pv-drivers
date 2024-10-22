function SignFile() {
    param (
        [Parameter(Mandatory)]
        [string]$SigningCertificateThumbprint,
        [Parameter()]
        [string[]]$FilePath
    )

    $SigningCertificate = Get-ChildItem Cert:\CurrentUser\My\$Env:SigningCertificateThumbprint
    $signArgs = @{
        HashAlgorithm = "SHA256"
        Certificate   = $SigningCertificate
    }
    if (![string]::IsNullOrEmpty($Env:TimestampServer)) {
        $signArgs["TimestampServer"] = $Env:TimestampServer
    }

    Set-AuthenticodeSignature -FilePath $FilePath @signArgs
}
