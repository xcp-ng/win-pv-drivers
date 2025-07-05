
<#PSScriptInfo

.VERSION 1.0

.GUID 5948fb9f-c421-4e12-82db-383de1663dca

.AUTHOR Xen Project

.COMPANYNAME

.COPYRIGHT

.TAGS

.LICENSEURI

.PROJECTURI

.ICONURI

.EXTERNALMODULEDEPENDENCIES

.REQUIREDSCRIPTS

.EXTERNALSCRIPTDEPENDENCIES

.RELEASENOTES


#>

<#

.DESCRIPTION
 XenClean invocation script

#>

#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param (
    [Parameter()]
    [switch]$NoReboot
)

$ErrorActionPreference = "Stop"

if (![Environment]::Is64BitProcess) {
    throw "XenClean cannot run in PowerShell x86!"
}

function Export-XenSettings {
    param($LogPath, $Suffix)

    Write-Host "Saving settings$Suffix..."

    & "$System32\pnputil.exe" /enum-drivers > "$LogPath/drivers$Suffix.log"
    if ([System.Environment]::OSVersion.Version -ge [version]::Parse("10.0.18362")) {
        & "$System32\pnputil.exe" /enum-devices /relations > "$LogPath/devices$Suffix.log"
    }

    & "$System32\reg.exe" export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e96a-e325-11ce-bfc1-08002be10318}" "$LogPath/hdc$Suffix.reg" /y >> "$LogPath/export$Suffix.log"
    & "$System32\reg.exe" export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e97d-e325-11ce-bfc1-08002be10318}" "$LogPath/system$Suffix.reg" /y >> "$LogPath/export$Suffix.log"
    foreach ($service in @("XEN", "xenbus", "xencons", "xenfilt", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd", "Tcpip", "Tcpip6")) {
        & "$System32\reg.exe" export "HKLM\SYSTEM\CurrentControlSet\Services\$Service" "$LogPath/service-$Service$Suffix.reg" /y >> "$LogPath/export$Suffix.log"
    }
    & "$System32\reg.exe" export "HKLM\SOFTWARE\XenOffboard" "$LogPath/xenoffboard$Suffix.reg" /y >> "$LogPath/export$Suffix.log"
}

if (!$PSCmdlet.ShouldProcess("Local computer", "Remove Xen drivers and tools")) {
    exit;
}

$System32 = [System.Environment]::SystemDirectory
$DirName = "xenclean-$(Get-Date -Format FileDateTime)"
$LogPath = "$env:TEMP\$DirName"
New-Item -ItemType Directory -Path $LogPath -Force

Export-XenSettings -LogPath $LogPath -Suffix "-pre"
Write-Host "Running XenClean, be patient..."
& "$PSScriptRoot\bin\XenClean.exe" > "$LogPath/xenclean.log"
Export-XenSettings -LogPath $LogPath -Suffix "-post"

Copy-Item -Recurse -ErrorAction SilentlyContinue $LogPath $Env:SystemDrive\$DirName

if (!$NoReboot) {
    Restart-Computer -Force
}
