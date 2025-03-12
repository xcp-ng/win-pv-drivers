
<#PSScriptInfo

.VERSION 1.0.0.0

.GUID 79322bc8-94fe-42f6-8b81-8373fa9458d0

.AUTHOR ngoc-tu.dinh@vates.tech

.COMPANYNAME Vates

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

.SYNOPSIS
 Detects and remedies XSA-468 / CVE-2025-27462, CVE-2025-27463.

.DESCRIPTION
 This script operates in one of three modes:

 - In the default mode, this script applies security controls to existing Xen devices and drivers to mitigate XSA-468, and disables the XenServer/XCP-ng "batch command" feature of the guest agent. Optionally, the `-Install` parameter installs the script to the current Windows installation, and registers it to run at every system boot via Task Scheduler.

 - The `-Scan` parameter scans for the existence of devices vulnerable to XSA-468, as well as the XenServer/XCP-ng "batch command" feature.

 - The `-Uninstall` parameter removes this script from the Task Scheduler library and prevents it from starting at system boot. Note that the security controls and batch command disablement will not be undone.

 The `-WhatIf` parameter can be used to simulate the execution of the default and `-Uninstall` modes.

#>

#Requires -RunAsAdministrator

using namespace System.Security.AccessControl
using namespace System.Security.Principal

[CmdletBinding(SupportsShouldProcess)]
param (
    # Scan for vulnerability.
    [Parameter(Mandatory, ParameterSetName = "Scan")][switch]$Scan,

    # Don't apply security controls to running drivers.
    [Parameter(ParameterSetName = "Install")][switch]$NoSecureObjects,
    # Don't apply security controls to drivers at boot time.
    [Parameter(ParameterSetName = "Install")][switch]$NoSetRegistry,
    # Don't disable Xen management agent's "batch command" feature.
    [Parameter(ParameterSetName = "Install")][switch]$NoDisableBatcmd,
    # Install this script to run at every boot.
    [Parameter(ParameterSetName = "Install")][switch]$Install,
    # For internal use only.
    [Parameter(ParameterSetName = "Install")][switch]$NoPinvokeAsJob,

    # Uninstall this script from the system.
    [Parameter(Mandatory, ParameterSetName = "Uninstall")][switch]$Uninstall
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
$Script:ScheduledTaskName = "XenToolsWorkaround"
$Script:InstallPath = "$env:SystemRoot\XenToolsWorkaround.ps1"
$Script:PowershellPath = Join-Path ([System.Environment]::SystemDirectory) "WindowsPowerShell\v1.0\powershell.exe"

$Script:RegistryPaths = @(
    "HKLM:\Software\XenServer\XenTools",
    "HKLM:\Software\Citrix\XenTools",
    "HKLM:\Software\XCP-ng\XenTools",
    "HKLM:\SOFTWARE\WOW6432Node\XenServer\XenTools",
    "HKLM:\SOFTWARE\WOW6432Node\Citrix\XenTools",
    "HKLM:\SOFTWARE\WOW6432Node\XCP-ng\XenTools"
)

# Only list SIDs that belong to the default insecure configuration
$Script:VulnerableSids = @(
    [SecurityIdentifier]::new([WellKnownSidType]::WorldSid, $null),
    [SecurityIdentifier]::new([WellKnownSidType]::RestrictedCodeSid, $null)
)

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

    # stage local variables so that they could be passed to remote jobs
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

        $ScriptBlock = [scriptblock] {
            param (
                [bool]$WIP,
                [string]$TypeDefinition,
                [string]$devicePath,
                [byte[]]$sdBytes
            )

            $ErrorActionPreference = "Stop"

            Write-Output "Loading helper types"
            Add-Type -TypeDefinition $TypeDefinition

            $deviceFilePath = Join-Path "\\.\GLOBALROOT" $devicePath
            # FileInfo.SetAccessControl explodes when it encounters a kernel object, so we have to use this method
            if ($WIP) {
                Write-Output "What if: Set DACL of $($sdBytes.Length) bytes on $deviceFilePath"
            }
            else {
                [XenToolsWorkaround.KernelObjects]::SetObjectDacl($deviceFilePath, $sdBytes)
                Write-Output "Successfully set DACL on $deviceFilePath"
            }
        }

        $jobArgs = @($WhatIfPreference, $Script:TypeDefinition, $devicePath, $sdBytes)
        if ($NoPinvokeAsJob) {
            & $ScriptBlock @jobArgs | Write-Verbose
        }
        else {
            # isolate the loaded types from the current session
            Start-Job $ScriptBlock -ArgumentList $jobArgs | `
                Receive-Job -Wait -AutoRemoveJob | `
                Write-Verbose
        }
    }
}

function Test-XenDeviceObjects {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)][string]$CompatibleIdType,
        [Parameter(Mandatory)][string]$CompatibleIdPattern,
        [Parameter()][string]$Class
    )

    $foundObjects = @();
    Get-XenDevice `
        -Class $Class `
        -CompatibleIdType $CompatibleIdType `
        -CompatibleIdPattern $CompatibleIdPattern | ForEach-Object {
        $deviceId = $_.DeviceID
        $devicePath = (Get-PnpDeviceProperty -InputObject $_ DEVPKEY_Device_PDOName).Data
        Write-Verbose "devicePath $devicePath"
        if ([string]::IsNullOrEmpty($devicePath)) {
            return
        }
        Write-Verbose "Testing $deviceId, devicePath = $devicePath"

        $deviceFilePath = Join-Path "\\.\GLOBALROOT" $devicePath
        $acl = Get-Acl $deviceFilePath
        Write-Verbose $acl.Sddl
        $acl.Access | Where-Object AccessControlType -eq Allow | ForEach-Object {
            if (!$_.IdentityReference.IsValidTargetType([SecurityIdentifier])) {
                continue
            }
            $subj = $_.IdentityReference.Translate([SecurityIdentifier])
            if ($Script:VulnerableSids | Where-Object { $_ -eq $subj }) {
                $foundObjects += @($deviceId)
            }
        }
    }
    return $foundObjects
}

function Test-XenBatCmdFeature {
    if ($null -eq (Get-Service XenSvc -ErrorAction SilentlyContinue)) {
        Write-Verbose "XenSvc doesn't exist"
        return $false
    }

    $vulnerable = $true
    foreach ($regpath in $Script:RegistryPaths) {
        # Get-ItemPropertyValue -ErrorAction SilentlyContinue doesn't really work, so we have to test the value's existence first
        if ($null -ne (Get-ItemProperty $regpath -Name NoRemoteExecution -ErrorAction SilentlyContinue)) {
            $value = Get-ItemPropertyValue $regpath -Name NoRemoteExecution -ErrorAction SilentlyContinue
            Write-Verbose "At ${regpath}: NoRemoteExecution = $value"
            if (0 -eq $value) {
                Write-Host "Batch command feature is intentionally enabled"
                return $true
            }
            else {
                $vulnerable = $false
            }
        }
    }
    return $vulnerable
}

if ($Scan) {
    $Script:IsVulnerable = $false

    Write-Host
    Write-Host "Looking for vulnerable objects"
    foreach ($devtype in $Script:DeviceTypes) {
        Test-XenDeviceObjects @devtype | ForEach-Object {
            Write-Host "Found vulnerable object $_"
            $Script:IsVulnerable = $true
        }
    }

    Write-Host
    Write-Host "Looking for vulnerable management agent"
    if (Test-XenBatCmdFeature) {
        Write-Host "Batch command feature is not disabled"
        $Script:IsVulnerable = $true
    }
    else {
        Write-Host "OK, batch command feature is disabled"
    }

    Write-Host
    if ($Script:IsVulnerable) {
        Write-Host "Found potential vulnerability, installation is recommended"
    }
    else {
        Write-Host "Did not find evidence of vulnerability, installation is not needed"
    }
}

elseif ($PSCmdlet.ParameterSetName -ieq "Install") {
    if (!$NoSecureObjects) {
        Write-Host
        Write-Host "Protecting active Xen device objects"
        foreach ($devtype in $Script:DeviceTypes) {
            Protect-XenDeviceObjects @devtype -SecurityDescriptor $SecurityDescriptor -WhatIf:$WhatIfPreference
        }
    }

    if (!$NoSetRegistry) {
        Write-Host
        Write-Host "Setting Xen device security registry values"
        foreach ($devtype in $Script:DeviceTypes) {
            # WS2016 (and maybe others) doesn't give administrators write rights to this key, only SYSTEM.
            # Even if it fails, it's not a critical failure as long as -Install is used, since the scheduled task starts as SYSTEM.
            try {
                Set-XenDriverSecurity @devtype -SecurityDescriptor $SecurityDescriptor -WhatIf:$WhatIfPreference
            }
            catch {
                $Script:SetRegistryFailed = $true
            }
        }
    }

    if (!$NoDisableBatcmd) {
        Write-Host
        Write-Host "Disabling batch command feature"
        foreach ($regpath in $Script:RegistryPaths) {
            Set-ItemProperty $regpath -Name NoRemoteExecution -Value 1 -Type DWord -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
        }
    }
}

if ($Install) {
    Write-Verbose "Current path: $PSCommandPath"
    Write-Verbose "Install path: $Script:InstallPath"

    Write-Host
    Write-Host "Installing"
    if ((Convert-Path $PSCommandPath -ErrorAction SilentlyContinue) -ieq (Convert-Path $Script:InstallPath -ErrorAction SilentlyContinue)) {
        Write-Host "Cannot install from already-installed script, abandoning"
    }
    else {
        Write-Host "Installing script to run at startup"

        Copy-Item $PSCommandPath -Destination $Script:InstallPath -Force -WhatIf:$WhatIfPreference

        $existingTask = Get-ScheduledTask -TaskName $Script:ScheduledTaskName -ErrorAction SilentlyContinue
        if ($null -ne $existingTask) {
            Write-Verbose "Scheduled task already installed, reinstalling"
            $existingTask | Unregister-ScheduledTask -Confirm:$false -WhatIf:$WhatIfPreference
        }

        $cmdArgs = @(
            # We're not running in a user shell, no need to worry about Add-Type contamination
            "-NoPinvokeAsJob",
            # No need to disable batcmd every single time in installed instance
            "-NoDisableBatcmd"
        )
        if ($NoSecureObjects) {
            $cmdArgs += @("-NoSecureObjects")
        }
        if ($NoSetRegistry) {
            $cmdArgs += @("-NoSetRegistry")
        }
        $argString = "-NoProfile -NonInteractive -ExecutionPolicy Bypass `"$Script:InstallPath`" $($cmdArgs -join ' ')"
        Write-Verbose "Task executable: $Script:PowershellPath"
        Write-Verbose "Task arguments: $argString"

        $task = New-ScheduledTask `
            -Action (New-ScheduledTaskAction -Execute $Script:PowershellPath -Argument $argString) `
            -Principal (New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -RunLevel Highest -LogonType ServiceAccount) `
            -Trigger (New-ScheduledTaskTrigger -AtStartup) `
            -Settings (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 5))

        # Register-ScheduledTask doesn't support -WhatIf
        if ($PSCmdlet.ShouldProcess($Script:ScheduledTaskName, "Create scheduled task")) {
            $task | Register-ScheduledTask -TaskName $Script:ScheduledTaskName | Out-Null
        }
    }
}
elseif ($Uninstall) {
    Write-Host
    Write-Host "Uninstalling scheduled task and files"
    Get-ScheduledTask -TaskName $Script:ScheduledTaskName -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false -WhatIf:$WhatIfPreference
    Remove-Item -Force -Path $Script:InstallPath -WhatIf:$WhatIfPreference -ErrorAction SilentlyContinue
}

Write-Host
if ($Script:SetRegistryFailed) {
    if ($Install) {
        Write-Host "Finished, you may need to reboot for the changes to take effect"
    }
    else {
        Write-Host "Failed to set registry entries, try using -Install instead"
    }
}
else {
    Write-Host "Finished"
}
