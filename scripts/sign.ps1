function Get-SignerObject() {
    if (!$Env:SIGNER) {
        return $null
    }

    if (${Env:SIGNER}.EndsWith(".pfx", [System.StringComparison]::OrdinalIgnoreCase)) {
        return Get-PfxCertificate -FilePath $Env:SIGNER
    }
    else {
        return Get-ChildItem Cert:\CurrentUser\My\$Env:SIGNER
    }
}

function Set-SignerFileSignature() {
    param (
        [Parameter()]
        [string[]]$FilePath
    )

    $SignerObject = Get-SignerObject
    if (!$SignerObject) {
        return
    }

    $signArgs = @{
        FilePath      = $FilePath
        HashAlgorithm = "SHA256"
        Certificate   = $SignerObject
    }
    if ($Env:TIMESTAMP_SERVER) {
        $signArgs["TimestampServer"] = $Env:TIMESTAMP_SERVER
    }

    Set-AuthenticodeSignature @signArgs
}

function Export-SignerCertificate {
    param (
        [Parameter()]
        [string[]]$OutDir
    )

    $SignerObject = Get-SignerObject
    if (!$SignerObject) {
        return
    }

    Export-Certificate -Cert $SignerObject -FilePath $OutDir\$($SignerObject.Thumbprint).crt -Type CERT -Force
}
