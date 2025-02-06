[CmdletBinding()]
param (
    [Parameter()]
    [string]$Target = "Rebuild",
    [Parameter(Mandatory)]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [string]$Platform,
    [Parameter()]
    [string]$OutDir = "$PSScriptRoot\output",
    [Parameter()]
    [switch]$ExportCertificate
)

$ErrorActionPreference = "Stop"

. $PSScriptRoot/branding.ps1
. $PSScriptRoot/branding-generic.ps1

msbuild.exe "$PSScriptRoot\installer\installer.sln" /t:$Target /p:Configuration=$Configuration /p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with error $LASTEXITCODE"
}

$VersionDir = "$OutDir\$(Get-PackageVersion Product)"
Remove-Item -Path $VersionDir -Force -Recurse -ErrorAction SilentlyContinue
if ($Target -ine "Clean") {
    $PackageDir = "$VersionDir\package"

    New-Item -Path $PackageDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\installer\bin\$Platform\$Configuration\en-US\*" -Destination $PackageDir\ -Force

    # XenClean
    $XenCleanDir = "$PackageDir\XenClean\$Platform"
    New-Item -Path $XenCleanDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\Invoke-XenClean.ps1" -Destination $XenCleanDir\ -Force

    New-Item -Path $XenCleanDir\bin -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\*" -Exclude Invoke-XenClean.ps1 -Destination $XenCleanDir\bin\ -Force

    # XenBootFix
    $XenBootFixDir = "$PackageDir\XenBootFix\$Platform"
    New-Item -Path $XenBootFixDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenBootFix\$Platform\$Configuration\XenBootFix.exe" -Destination $XenBootFixDir\ -Force

    if ($ExportCertificate) {
        $TestsignDir = "$VersionDir\testsign"

        New-Item -Path $TestsignDir -ItemType Directory -Force
        Copy-Item -Path "$PSScriptRoot\testsign\install.ps1" -Destination $TestsignDir\ -Force
        Export-Certificate -Cert Cert:\CurrentUser\My\${Env:SIGNER_THUMBPRINT} -FilePath $TestsignDir\${Env:SIGNER_THUMBPRINT}.crt -Type CERT -Force
    }
}
