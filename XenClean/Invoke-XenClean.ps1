
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

if (!$PSCmdlet.ShouldProcess("Local computer", "Remove Xen drivers and tools")) {
    exit;
}

$Timestamp = Get-Date -Format FileDateTime
$LogPath = "$env:TEMP\xenclean-$Timestamp"
New-Item -ItemType Directory -Path $LogPath -Force

& "$PSScriptRoot\bin\XenClean.exe" > "$LogPath/xenclean.log"
pnputil.exe /enum-drivers > "$LogPath/drivers.log"
if ([System.Environment]::OSVersion.Version -ge [version]::Parse("10.0.18362")) {
    pnputil.exe /enum-devices /relations > "$LogPath/devices.log"
}
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e96a-e325-11ce-bfc1-08002be10318}" "$LogPath/hdc.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e97d-e325-11ce-bfc1-08002be10318}" "$LogPath/system.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Services\XEN" "$LogPath/service-xen.reg" /y

if (!$NoReboot) {
    Restart-Computer -Force
}
