[CmdletBinding(SupportsShouldProcess, ConfirmImpact="High")]
param (
    [Parameter()]
    [switch]$Reboot
)

$ErrorActionPreference = "Stop"

if (!$PSCmdlet.ShouldProcess("Local computer", "Remove Xen drivers and tools")) {
    exit;
}

$Timestamp = Get-Date -Format FileDateTime
$LogPath = "xenclean-$Timestamp"
New-Item -ItemType Directory -Path $LogPath -Force

& "$PSScriptRoot\XenClean.exe" > "$LogPath/xenclean.log"
pnputil.exe /enum-drivers > "$LogPath/drivers.log"
if ([System.Environment]::OSVersion.Version -ge [version]::Parse("10.0.18362")) {
    pnputil.exe /enum-devices /relations > "$LogPath/devices.log"
}
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e96a-e325-11ce-bfc1-08002be10318}" "$LogPath/hdc.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e97d-e325-11ce-bfc1-08002be10318}" "$LogPath/system.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Services\xenbus" "$LogPath/service-xenbus.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Services\xenbus_monitor" "$LogPath/service-xenbus_monitor.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Services\xenfilt" "$LogPath/service-xenfilt.reg" /y

if ($Reboot) {
    Restart-Computer -Force
}
