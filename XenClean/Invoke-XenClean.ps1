[CmdletBinding()]
param (
    [Parameter()]
    [switch]$Reboot
)

$ErrorActionPreference = "Stop"

$Timestamp = Get-Date -Format FileDateTime
& "$PSScriptRoot\XenClean.exe" > "xenclean-$Timestamp.log"
pnputil.exe /enum-drivers > "drivers-$Timestamp.log"
if ([System.Environment]::OSVersion.Version -ge [version]::Parse("10.0.18362")) {
    pnputil.exe /enum-devices /relations > "devices-$Timestamp.log"
}
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e96a-e325-11ce-bfc1-08002be10318}" "hdc-$timestamp.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e97d-e325-11ce-bfc1-08002be10318}" "system-$timestamp.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Services\xenbus" "service-xenbus-$timestamp.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Services\xenbus_monitor" "service-xenbus_monitor-$timestamp.reg" /y
reg.exe export "HKLM\SYSTEM\CurrentControlSet\Services\xenfilt" "service-xenfilt-$timestamp.reg" /y

if ($Reboot) {
    Restart-Computer -Force
}
