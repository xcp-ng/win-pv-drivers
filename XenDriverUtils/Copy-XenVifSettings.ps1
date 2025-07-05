# SPDX-License-Identifier: BSD-2-Clause

# Attention, this script is branding-agnostic!

#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, ParameterSetName = "Invoke")]
    [switch]$Invoke,
    # Install scheduled task to run on reboot.
    [Parameter(Mandatory, ParameterSetName = "Install")]
    [switch]$Install,

    [Parameter(Mandatory)]
    [ValidateSet("Backup", "Restore")]
    [string]$Mode,

    # Delete script after running.
    [Parameter(ParameterSetName = "Invoke")]
    [switch]$SelfDestruct
)

$ErrorActionPreference = "Stop"

if (![Environment]::Is64BitProcess) {
    throw "Cannot run this script from PowerShell x86!"
}

$Script:ScheduledTaskName = "Copy-XenVifSettings"
$Script:InstallPath = "$env:ProgramFiles\Copy-XenVifSettings.ps1"
$Script:PowershellPath = Join-Path ([System.Environment]::SystemDirectory) "WindowsPowerShell\v1.0\powershell.exe"

function Backup-XenVifSettings {
    [CmdletBinding(SupportsShouldProcess)]
    param (
        [Parameter(Mandatory)][string]$InterfaceGuid,
        [Parameter(Mandatory)][string]$PermanentAddress
    )

    Write-Verbose "Backing up $PermanentAddress = $InterfaceGuid"

    Remove-Item -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip -Force -Recurse -ErrorAction SilentlyContinue -WhatIf:$WhatIfPreference
    New-Item -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip -Force -WhatIf:$WhatIfPreference
    Get-ItemProperty -Path HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$InterfaceGuid -ErrorAction Continue | `
        Set-ItemProperty -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip -ErrorAction Continue -WhatIf:$WhatIfPreference

    Remove-Item -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip6 -Force -Recurse -ErrorAction SilentlyContinue -WhatIf:$WhatIfPreference
    New-Item -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip6 -Force -WhatIf:$WhatIfPreference
    Get-ItemProperty -Path HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\$InterfaceGuid -ErrorAction Continue | `
        Set-ItemProperty -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip6 -ErrorAction Continue -WhatIf:$WhatIfPreference
}

function Restore-XenVifSettings {
    [CmdletBinding(SupportsShouldProcess)]
    param (
        [Parameter(Mandatory)][string]$InterfaceGuid,
        [Parameter(Mandatory)][string]$PermanentAddress
    )

    $Restored = $false

    if (Test-Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip) {
        Write-Verbose "$PermanentAddress = $InterfaceGuid has Tcpip"

        Get-ItemProperty -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip -ErrorAction Continue | `
            Set-ItemProperty -Path HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$InterfaceGuid -ErrorAction Continue -WhatIf:$WhatIfPreference

        $Restored = $true
    }

    if (Test-Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip6) {
        Write-Verbose "$PermanentAddress = $InterfaceGuid has Tcpip6"

        Get-ItemProperty -Path HKLM:\SOFTWARE\XenOffboard\Xenvif\$PermanentAddress\Tcpip6 -ErrorAction Continue | `
            Set-ItemProperty -Path HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\$InterfaceGuid -ErrorAction Continue -WhatIf:$WhatIfPreference

        $Restored = $true
    }

    if ($Restored) {
        # reload settings from registry
        Get-NetAdapter | Where-Object InterfaceGuid -ieq $InterfaceGuid | `
            Disable-NetAdapter -Confirm:$false -PassThru -WhatIf:$WhatIfPreference | `
            Enable-NetAdapter -Confirm:$false -WhatIf:$WhatIfPreference
    }
}

if ($PSCmdlet.ParameterSetName -ieq "Invoke") {
    try {
        if ($Mode -ieq "Backup") {
            Remove-Item -Path HKLM:\SOFTWARE\XenOffboard\Xenvif -Force -Recurse -ErrorAction SilentlyContinue -WhatIf:$WhatIfPreference
            $MatchingInterfaces = Get-NetAdapter | Where-Object PnPDeviceID -like "XENVIF\*"
            $MatchingInterfaces | ForEach-Object {
                Backup-XenVifSettings -InterfaceGuid $_.InterfaceGuid -PermanentAddress $_.PermanentAddress -WhatIf:$WhatIfPreference
            }
        }
        elseif ($Mode -ieq "Restore") {
            Get-NetAdapter | ForEach-Object {
                Restore-XenVifSettings -InterfaceGuid $_.InterfaceGuid -PermanentAddress $_.PermanentAddress -WhatIf:$WhatIfPreference
            }
        }
    }
    finally {
        if ($SelfDestruct) {
            Get-ScheduledTask -TaskName $Script:ScheduledTaskName -ErrorAction SilentlyContinue | `
                Unregister-ScheduledTask -Confirm:$false -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
            Remove-Item $PSCommandPath -Force -WhatIf:$WhatIfPreference
        }
    }
}

elseif ($PSCmdlet.ParameterSetName -ieq "Install") {
    Write-Verbose "Current path: $PSCommandPath"
    Write-Verbose "Install path: $Script:InstallPath"

    if ((Convert-Path $PSCommandPath -ErrorAction SilentlyContinue) -ieq (Convert-Path $Script:InstallPath -ErrorAction SilentlyContinue)) {
        throw "Cannot install from already-installed script, abandoning"
    }

    Copy-Item $PSCommandPath -Destination $Script:InstallPath -Force -WhatIf:$WhatIfPreference

    $existingTask = Get-ScheduledTask -TaskName $Script:ScheduledTaskName -ErrorAction SilentlyContinue
    if ($null -ne $existingTask) {
        Write-Verbose "Scheduled task is already installed, reinstalling"
        $existingTask | Unregister-ScheduledTask -Confirm:$false -WhatIf:$WhatIfPreference
    }

    $cmdArgs = @(
        "-Invoke",
        "-Mode",
        $Mode,
        "-SelfDestruct"
    )
    $argString = "-NoProfile -NonInteractive -ExecutionPolicy Bypass `"& '$Script:InstallPath' $($cmdArgs -join ' ')`""
    Write-Verbose "Task executable: $Script:PowershellPath"
    Write-Verbose "Task arguments: $argString"

    $task = New-ScheduledTask `
        -Trigger (New-ScheduledTaskTrigger -AtStartup) `
        -Action (New-ScheduledTaskAction -Execute $Script:PowershellPath -Argument $argString) `
        -Principal (New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -RunLevel Highest -LogonType ServiceAccount) `
        -Settings (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 5))
    if ($PSCmdlet.ShouldProcess($Script:ScheduledTaskName, "Create scheduled task")) {
        $task | Register-ScheduledTask -TaskName $Script:ScheduledTaskName
    }
}
