[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "Low")]
param()

$ErrorActionPreference = "Stop"
$Vendor = "XCP-ng"
$XenTools = "$PSScriptRoot\XenTools-x64.msi"

function Checkpoint-RegistryValue {
    [CmdletBinding(SupportsShouldProcess)]
    param (
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name,
        [Parameter()][switch]$Force
    )

    $Value = Get-ItemProperty $Path -Name $Name -ErrorAction SilentlyContinue | `
        Select-Object -ExpandProperty $Name
    $SavedValue = Get-ItemProperty HKLM:\SOFTWARE\XenOffboard\$Category -Name $Name -ErrorAction SilentlyContinue | `
        Select-Object -ExpandProperty $Name
    if ($null -ne $Value -and ($Force -or $null -eq $SavedValue)) {
        New-Item -Path HKLM:\SOFTWARE\XenOffboard\$Category -Force -WhatIf:$WhatIfPreference
        Set-ItemProperty HKLM:\SOFTWARE\XenOffboard\$Category -Name $Name -Value $Value -ErrorAction SilentlyContinue -WhatIf:$WhatIfPreference
    }
}

function Restore-RegistryValue {
    [CmdletBinding(SupportsShouldProcess)]
    param (
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    $SavedValue = Get-ItemProperty HKLM:\SOFTWARE\XenOffboard\$Category -Name $Name -ErrorAction SilentlyContinue | `
        Select-Object -ExpandProperty $Name
    if ($null -ne $SavedValue) {
        Set-ItemProperty $Path -Name $Name -Value $Value -Force -WhatIf:$WhatIfPreference
    }
}

Checkpoint-RegistryValue `
    -Category Onboard `
    -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata" `
    -Name PreventDeviceMetadataFromNetwork `
    -WhatIf:$WhatIfPreference
Checkpoint-RegistryValue `
    -Category Onboard `
    -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" `
    -Name ExcludeWUDriversInQualityUpdate `
    -WhatIf:$WhatIfPreference
Checkpoint-RegistryValue `
    -Category Onboard `
    -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DriverSearching" `
    -Name SearchOrderConfig `
    -WhatIf:$WhatIfPreference

New-Item `
    -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata" `
    -Force `
    -WhatIf:$WhatIfPreference | Out-Null
Set-ItemProperty `
    -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata" `
    -Name PreventDeviceMetadataFromNetwork `
    -Type DWord `
    -Value 1 `
    -Force `
    -WhatIf:$WhatIfPreference
New-Item `
    -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" `
    -Force `
    -WhatIf:$WhatIfPreference | Out-Null
Set-ItemProperty `
    -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" `
    -Name ExcludeWUDriversInQualityUpdate `
    -Type DWord `
    -Value 1 `
    -Force `
    -WhatIf:$WhatIfPreference
New-Item `
    -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DriverSearching" `
    -Force `
    -WhatIf:$WhatIfPreference | Out-Null
Set-ItemProperty `
    -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DriverSearching" `
    -Name SearchOrderConfig `
    -Type DWord `
    -Value 0 `
    -Force `
    -WhatIf:$WhatIfPreference

$xencleanArgs = @(
    "-noConfirm",
    "-noReboot",
    "-onboard",
    $Vendor
)
if ($WhatIfPreference) {
    $xencleanArgs += @("-dryRun")
}

& "$PSScriptRoot\XenClean.exe" @xencleanArgs
switch ($LASTEXITCODE) {
    0 {
        # cleaning succeeded; shut down here to allow the user to disable the WU option
        Stop-Computer -Force -WhatIf:$WhatIfPreference
    }
    64 {
        # ready for onboarding, we can install now

        Restore-RegistryValue `
            -Category Onboard `
            -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata" `
            -Name PreventDeviceMetadataFromNetwork `
            -WhatIf:$WhatIfPreference
        Restore-RegistryValue `
            -Category Onboard `
            -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" `
            -Name ExcludeWUDriversInQualityUpdate `
            -WhatIf:$WhatIfPreference
        Restore-RegistryValue `
            -Category Onboard `
            -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DriverSearching" `
            -Name SearchOrderConfig `
            -WhatIf:$WhatIfPreference
        Remove-Item `
            HKLM:\SOFTWARE\XenOffboard\Onboard `
            -Force `
            -ErrorAction SilentlyContinue `
            -WhatIf:$WhatIfPreference

        if (!$WhatIfPreference) {
            $msiexec = Start-Process `
                -Wait "$env:windir\system32\msiexec.exe" `
                -ArgumentList "/i", $XenTools, "/passive", "/norestart", "/log", "$env:TEMP\xentools-onboard.log" `
                -PassThru
            if ($msiexec.ExitCode -ne 0 -and $msiexec.ExitCode -ne 3010) {
                exit $msiexec.ExitCode
            }

            $signature = @'
[DllImport("Cfgmgr32.dll")]
public static extern uint CMP_WaitNoPendingInstallEvents(uint dwTimeout);
public const uint INFINITE = 0xFFFFFFFF;
public const uint WAIT_OBJECT_0 = 0;
public const uint WAIT_TIMEOUT = 258;
public const uint WAIT_FAILED = 0xFFFFFFFF;
'@
            $nativeMethods = Add-Type -MemberDefinition $signature -Name NativeMethods -Namespace XenTools -PassThru

            Start-Sleep -Seconds 15
            Write-Output "Waiting for install events"
            $nativeMethods::CMP_WaitNoPendingInstallEvents($nativeMethods::INFINITE)

            Restart-Computer -Force
        }
    }
    65 {
        # already onboarded
        exit 0
    }
    66 {
        # onboard denied
        Write-Error "Onboard denied, check XenClean logs for details"
        exit 66
    }
    default {
        exit $LASTEXITCODE
    }
}
