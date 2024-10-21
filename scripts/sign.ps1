function SignFile() {
    param (
        [Parameter()]
        [string[]]$FilePath
    )

    $signArgs = @{
        HashAlgorithm = "SHA256"
        Certificate = $Script:SigningCertificate
    }
    if (![string]::IsNullOrEmpty($Env:TimestampServer)) {
        $signArgs["TimestampServer"] = $Env:TimestampServer
    }

    Set-AuthenticodeSignature -FilePath $FilePath @signArgs
}
