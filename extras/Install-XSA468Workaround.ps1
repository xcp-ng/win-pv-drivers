
<#PSScriptInfo

.VERSION 1.0.0.0

.GUID 79322bc8-94fe-42f6-8b81-8373fa9458d0

.AUTHOR ngoc-tu.dinh@vates.tech

.COMPANYNAME Vates

.COPYRIGHT

.TAGS

.LICENSEURI

.PROJECTURI https://xenbits.xen.org/xsa/advisory-468.html

.ICONURI

.EXTERNALMODULEDEPENDENCIES

.REQUIREDSCRIPTS

.EXTERNALSCRIPTDEPENDENCIES

.RELEASENOTES


#>

<#

.SYNOPSIS
 Mitigates XSA-468 / CVE-2025-27462, CVE-2025-27463.

.DESCRIPTION
 This script applies security controls to existing Xen devices and drivers to mitigate XSA-468.

.LINK
 https://xenbits.xen.org/xsa/advisory-468.html

#>

#Requires -RunAsAdministrator

using namespace System.Security.AccessControl
using namespace System.Security.Principal

[CmdletBinding(SupportsShouldProcess)]
param (
    # Don't apply security controls to running drivers.
    [Parameter(ParameterSetName = "Install")]
    [Parameter(ParameterSetName = "Invoke")]
    [switch]$NoSecureObjects,
    # Don't apply security controls to drivers at boot time.
    [Parameter(ParameterSetName = "Install")]
    [Parameter(ParameterSetName = "Invoke")]
    [switch]$NoSetRegistry,

    # For internal use only.
    [Parameter(Mandatory, ParameterSetName = "Invoke")][switch]$Invoke
)

$ErrorActionPreference = "Stop"

if (![Environment]::Is64BitProcess) {
    throw "Cannot run this script from PowerShell x86!"
}

$Script:TypeDefinition = @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace XenToolsWorkaround {
    public static class KernelObjects {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        const uint WRITE_DAC = 0x00040000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING = 3;

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool SetKernelObjectSecurity(
            SafeHandle Handle,
            uint SecurityInformation,
            [In] byte[] SecurityDescriptor);

        const uint DACL_SECURITY_INFORMATION = 0x00000004;

        public static void SetObjectDacl(string path, byte[] sdBytes) {
            using (var handle = CreateFile(
                path,
                WRITE_DAC,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero)) {
                if (handle.IsInvalid) {
                    throw new Win32Exception();
                }
                if (!SetKernelObjectSecurity(handle, DACL_SECURITY_INFORMATION, sdBytes)) {
                    throw new Win32Exception();
                }
            }
        }
    }
}
"@

$Script:Sddl = "D:P(A;;GA;;;SY)(A;;GA;;;BA)"
$Script:SecurityDescriptor = (ConvertFrom-SddlString $Script:Sddl).RawDescriptor
$Script:ScheduledTaskName = "XSA468Workaround"
$Script:InstallPath = "$env:ProgramFiles\XSA468Workaround.ps1"
$Script:PowershellPath = Join-Path ([System.Environment]::SystemDirectory) "WindowsPowerShell\v1.0\powershell.exe"

# Prepackaged arguments for each device type
$Script:DeviceTypes = @(
    @{
        Class               = "System";
        CompatibleIdType    = "xenclass";
        CompatibleIdPattern = '*&DEV_IFACE&*'
    },
    @{
        CompatibleIdType    = "xendevice";
        CompatibleIdPattern = '*&DEV_CONSOLE*'
    }
)

function Get-SddlBytes {
    param (
        [Parameter(Mandatory)][CommonSecurityDescriptor]$SecurityDescriptor
    )

    $sdbytes = [byte[]]::new($SecurityDescriptor.BinaryLength)
    $SecurityDescriptor.GetBinaryForm($sdbytes, 0)
    return $sdbytes
}

function Get-XenDevice {
    param (
        [Parameter(Mandatory)][string]$CompatibleIdType,
        [Parameter(Mandatory)][string]$CompatibleIdPattern,
        [Parameter()][string]$Class
    )

    $pnpDeviceArgs = @{}
    if (![string]::IsNullOrEmpty($Class)) {
        $pnpDeviceArgs["Class"] = $Class
    }
    Get-PnpDevice -PresentOnly @pnpDeviceArgs | Where-Object {
        $cid = (Get-PnpDeviceProperty -InputObject $_ DEVPKEY_Device_CompatibleIds).Data
        $cid -icontains $CompatibleIdType -and !!($cid | Where-Object { $_ -ilike $CompatibleIdPattern })
    }
}

function Set-XenDriverSecurity {
    [CmdletBinding(SupportsShouldProcess)]
    param (
        [Parameter(Mandatory)][string]$CompatibleIdType,
        [Parameter(Mandatory)][string]$CompatibleIdPattern,
        [Parameter()][string]$Class,
        [Parameter(Mandatory)][CommonSecurityDescriptor]$SecurityDescriptor
    )

    Get-XenDevice `
        -Class $Class `
        -CompatibleIdType $CompatibleIdType `
        -CompatibleIdPattern $CompatibleIdPattern | ForEach-Object {
        $devid = $_.DeviceID
        Write-Verbose "Securing $($_.DeviceID)"
        $regpath = Join-Path HKLM:\SYSTEM\CurrentControlSet\Enum $devid
        Write-Verbose "regpath: $regpath"
        if ($PSCmdlet.ShouldProcess($regpath, "Set security")) {
            if ($null -eq (Get-ItemProperty $regpath -Name Security -ErrorAction SilentlyContinue)) {
                Set-ItemProperty $regpath -Name Security -Value ([byte[]](Get-SddlBytes -SecurityDescriptor $SecurityDescriptor)) -WhatIf:$WhatIfPreference
            }
            else {
                Write-Verbose "Device $devid already has Security value, skipping"
            }
        }
    }
}

function Protect-XenDeviceObjects {
    [CmdletBinding(SupportsShouldProcess)]
    param (
        [Parameter(Mandatory)][string]$CompatibleIdType,
        [Parameter(Mandatory)][string]$CompatibleIdPattern,
        [Parameter()][string]$Class,
        [Parameter(Mandatory)][CommonSecurityDescriptor]$SecurityDescriptor
    )

    $sdBytes = [byte[]](Get-SddlBytes -SecurityDescriptor $SecurityDescriptor)

    Get-XenDevice `
        -Class $Class `
        -CompatibleIdType $CompatibleIdType `
        -CompatibleIdPattern $CompatibleIdPattern | ForEach-Object {
        $deviceId = $_.DeviceID
        $devicePath = (Get-PnpDeviceProperty -InputObject $_ DEVPKEY_Device_PDOName).Data
        if ([string]::IsNullOrEmpty($devicePath)) {
            return
        }
        Write-Verbose "Protecting $deviceId, devicePath = $devicePath"

        Write-Verbose "Loading helper types"
        Add-Type -TypeDefinition $Script:TypeDefinition

        $deviceFilePath = Join-Path "\\.\GLOBALROOT" $devicePath
        # FileInfo.SetAccessControl explodes when it encounters a kernel object, so we have to use this method
        if ($PSCmdlet.ShouldProcess($deviceFilePath, "Set DACL")) {
            [XenToolsWorkaround.KernelObjects]::SetObjectDacl($deviceFilePath, $sdBytes)
            Write-Verbose "Successfully set DACL on $deviceFilePath"
        }
    }
}

if ($PSCmdlet.ParameterSetName -ieq "Invoke") {
    if (!$NoSecureObjects) {
        Write-Host
        Write-Host "Protecting active Xen device objects"
        foreach ($devtype in $Script:DeviceTypes) {
            try {
                Protect-XenDeviceObjects @devtype -SecurityDescriptor $SecurityDescriptor -WhatIf:$WhatIfPreference
            }
            catch {
                Write-Error $_
            }
        }
    }

    if (!$NoSetRegistry) {
        Write-Host
        Write-Host "Setting Xen device security registry values"
        foreach ($devtype in $Script:DeviceTypes) {
            try {
                Set-XenDriverSecurity @devtype -SecurityDescriptor $SecurityDescriptor -WhatIf:$WhatIfPreference
            }
            catch {
                Write-Error $_
            }
        }
    }
}

elseif ($PSCmdlet.ParameterSetName -ieq "Install") {
    Write-Verbose "Current path: $PSCommandPath"
    Write-Verbose "Install path: $Script:InstallPath"

    Write-Host
    Write-Host "Running script as SYSTEM"
    if ((Convert-Path $PSCommandPath -ErrorAction SilentlyContinue) -ieq (Convert-Path $Script:InstallPath -ErrorAction SilentlyContinue)) {
        Write-Host "Cannot install from already-installed script, abandoning"
    }
    else {
        # copy the script to a secure location for the SYSTEM task
        Copy-Item $PSCommandPath -Destination $Script:InstallPath -Force -WhatIf:$WhatIfPreference

        $existingTask = Get-ScheduledTask -TaskName $Script:ScheduledTaskName -ErrorAction SilentlyContinue
        if ($null -ne $existingTask) {
            Write-Verbose "Scheduled task already installed, reinstalling"
            $existingTask | Unregister-ScheduledTask -Confirm:$false -WhatIf:$WhatIfPreference
        }

        $cmdArgs = @(
            "-Invoke"
        )
        if ($NoSecureObjects) {
            $cmdArgs += @("-NoSecureObjects")
        }
        if ($NoSetRegistry) {
            $cmdArgs += @("-NoSetRegistry")
        }
        $argString = "-NoProfile -NonInteractive -ExecutionPolicy Bypass `"& '$Script:InstallPath' $($cmdArgs -join ' ')`""
        Write-Verbose "Task executable: $Script:PowershellPath"
        Write-Verbose "Task arguments: $argString"

        $task = New-ScheduledTask `
            -Action (New-ScheduledTaskAction -Execute $Script:PowershellPath -Argument $argString) `
            -Principal (New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -RunLevel Highest -LogonType ServiceAccount) `
            -Settings (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 5))

        # Register-ScheduledTask doesn't support -WhatIf
        if ($PSCmdlet.ShouldProcess($Script:ScheduledTaskName, "Create scheduled task")) {
            Write-Host "Starting mitigation task"
            $registeredTask = $task | Register-ScheduledTask -TaskName $Script:ScheduledTaskName
            $registeredTask | Start-ScheduledTask | Out-Null

            Write-Host "Waiting for task to finish"
            # wait for a max of 5 minutes before cleaning up
            for ($i = 0; $i -lt 60; $i++) {
                Start-Sleep -Seconds 5
                if (($registeredTask | Get-ScheduledTask).State -ine "Running") {
                    break
                }
            }

            Write-Host "Cleaning up"
            $registeredTask | Unregister-ScheduledTask -Confirm:$false -ErrorAction Continue
            Remove-Item $Script:InstallPath -Force -ErrorAction Continue
        }
    }
}

Write-Host
Write-Host "Finished"
