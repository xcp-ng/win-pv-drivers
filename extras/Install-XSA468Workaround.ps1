# SPDX-License-Identifier: BSD-2-Clause

# IMPORTANT NOTE: This script only covers vulnerabilities in XenIface.

<#PSScriptInfo

.VERSION 1.1.0.0

.GUID 79322bc8-94fe-42f6-8b81-8373fa9458d0

.AUTHOR ngoc-tu.dinh@vates.tech

.COMPANYNAME Vates

.COPYRIGHT (c) 2025 Vates.

.LICENSEURI https://spdx.org/licenses/BSD-2-Clause.html

.PROJECTURI https://xenbits.xen.org/xsa/advisory-468.html

.RELEASENOTES
2025-04-29 Initial release - 1.0.0.0
2025-06-02 Windows 7/PowerShell 2.0 compatibility - 1.1.0.0

#>

<#

.SYNOPSIS

Detects and mitigates XSA-468 (XenIface only).

.DESCRIPTION

This script applies security controls to existing XenIface devices and drivers to mitigate XSA-468. No reboot is needed.

This script also provides a detection function for XenIface devices vulnerable to XSA-468 using the -Scan switch.

IMPORTANT NOTE: This script does not cover vulnerabilities in XenBus and XenCons.

.EXAMPLE

.\Install-XSA468Workaround.ps1 -Scan
Looking for vulnerable XenIface objects
Found vulnerable object XENBUS\VEN_XS0002&DEV_IFACE\_
Found vulnerable object XENBUS\VEN_XS0002&DEV_IFACE\_
Found XenIface vulnerability, it's recommended to run the script
True

This example shows how to detect XenIface devices vulnerable to XSA-468 using this script.
In this example, the script reports that XenIface is vulnerable to XSA-468.

.EXAMPLE

.\Install-XSA468Workaround.ps1 -Scan
Looking for vulnerable XenIface objects
Did not find evidence of XenIface vulnerability
False

This example shows how to detect XenIface devices vulnerable to XSA-468 using this script.
In this example, the script reports that XenIface was not determined to be vulnerable to XSA-468.

.EXAMPLE

.\Install-XSA468Workaround.ps1
Running script as SYSTEM
Deleting old task (please ignore next error)
ERROR: The system cannot find the file specified.
Starting mitigation task
SUCCESS: The scheduled task "XSA468Workaround" has successfully been created.
SUCCESS: Attempted to run the scheduled task "XSA468Workaround".
Waiting for task to finish
Getting task status
Last Result:                          0
Cleaning up
SUCCESS: The scheduled task "XSA468Workaround" was successfully deleted.

This example shows how to use this script to apply XSA-468 mitigations to XenIface on a running system.
The script reports whether it succeeded (see "Last Result").

.LINK

https://xenbits.xen.org/xsa/advisory-468.html

#>

#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = "Install")]
param (
    # Scan for XenIface vulnerability.
    [Parameter(Mandatory = $true, ParameterSetName = "Scan")][switch]$Scan,

    # Don't apply security controls to running devices.
    [Parameter(ParameterSetName = "Install")]
    [Parameter(ParameterSetName = "Invoke")]
    [switch]$NoSecureObjects,
    # Don't apply security controls to installed drivers.
    [Parameter(ParameterSetName = "Install")]
    [Parameter(ParameterSetName = "Invoke")]
    [switch]$NoSetRegistry,

    # For internal use only.
    [Parameter(Mandatory = $true, ParameterSetName = "Invoke")][switch]$Invoke
)

$PSNativeCommandArgumentPassing = "Legacy"
$ErrorActionPreference = "Stop"

$Script:TypeDefinition = @"
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace XSA468Workaround {
    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVINFO_DATA {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public UIntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DEVPROPKEY {
        public Guid fmtid;
        public uint pid;
    }

    public static class NativeMethods {
        const uint READ_CONTROL = 0x00020000;
        const uint WRITE_DAC = 0x00040000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING = 3;
        const uint DACL_SECURITY_INFORMATION = 0x00000004;
        const int ERROR_INSUFFICIENT_BUFFER = 122;
        const int ERROR_NOT_FOUND = 1168;
        const uint DEVPROP_TYPE_STRING = 18;

        static DEVPROPKEY DEVPKEY_Device_PDOName() {
            DEVPROPKEY devpkey = new DEVPROPKEY();
            devpkey.fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0);
            devpkey.pid = 16;
            return devpkey;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetKernelObjectSecurity(
            SafeHandle Handle,
            uint RequestedInformation,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
            byte[] pSecurityDescriptor,
            uint nLength,
            out uint lpnLengthNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool SetKernelObjectSecurity(
            SafeHandle Handle,
            uint SecurityInformation,
            [In] byte[] SecurityDescriptor);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiCreateDeviceInfoList(
            IntPtr ClassGuid,
            IntPtr hwndParent);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool SetupDiOpenDeviceInfoW(
            IntPtr DeviceInfoSet,
            string DeviceInstanceId,
            IntPtr hwndParent,
            uint OpenFlags,
            ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool SetupDiGetDevicePropertyW(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            [In] ref DEVPROPKEY PropertyKey,
            out uint PropertyType,
            [Out] StringBuilder PropertyBuffer,
            uint PropertyBufferSize,
            out uint RequiredSize,
            uint Flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        delegate bool IsWow64ProcessDelegate(IntPtr hProcess, out bool Wow64Process);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetCurrentProcess();

        public static byte[] GetObjectDacl(string path) {
            using (SafeFileHandle handle = CreateFileW(
                path,
                READ_CONTROL,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero)) {
                if (handle.IsInvalid) {
                    throw new Win32Exception();
                }
                uint count = 0;
                if (!GetKernelObjectSecurity(handle, DACL_SECURITY_INFORMATION, null, 0, out count) &&
                    Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER) {
                    throw new Win32Exception();
                }
                byte[] sdBytes = new byte[count];
                if (!GetKernelObjectSecurity(handle, DACL_SECURITY_INFORMATION, sdBytes, count, out count)) {
                    throw new Win32Exception();
                }
                byte[] realSdBytes = new byte[count];
                Array.Copy(sdBytes, realSdBytes, count);
                return realSdBytes;
            }
        }

        public static void SetObjectDacl(string path, byte[] sdBytes) {
            using (SafeFileHandle handle = CreateFileW(
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

        static string GetDeviceProperty(IntPtr devInfo, ref SP_DEVINFO_DATA devInfoData, DEVPROPKEY devpkey) {
            uint requiredBytes;
            uint ptype;
            if (!SetupDiGetDevicePropertyW(
                    devInfo,
                    ref devInfoData,
                    ref devpkey,
                    out ptype,
                    null,
                    0,
                    out requiredBytes,
                    0)
                || ptype != DEVPROP_TYPE_STRING) {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_INSUFFICIENT_BUFFER) {
                    ; // expected error, continue to next step
                } else if (err == ERROR_NOT_FOUND) {
                    // expected error, device doesn't have property
                    return null;
                } else {
                    throw new Win32Exception(err, "SetupDiGetDeviceProperty " + err.ToString());
                }
            }
            StringBuilder buf = new StringBuilder((int)(requiredBytes / sizeof(char)));
            if (!SetupDiGetDevicePropertyW(
                    devInfo,
                    ref devInfoData,
                    ref devpkey,
                    out ptype,
                    buf,
                    (uint)buf.Capacity * sizeof(char),
                    out requiredBytes,
                    0)) {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "SetupDiGetDeviceProperty " + err.ToString());
            }
            return buf.ToString();
        }

        public static string GetDevicePdoName(string InstanceId) {
            IntPtr devInfo = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
            if (devInfo == (IntPtr)(-1)) {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "SetupDiCreateDeviceInfoList " + err.ToString());
            }
            try {
                SP_DEVINFO_DATA devInfoData = new SP_DEVINFO_DATA();
                devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);
                if (!SetupDiOpenDeviceInfoW(devInfo, InstanceId, IntPtr.Zero, 0, ref devInfoData)) {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, "SetupDiOpenDeviceInfoW " + err.ToString());
                }

                DEVPROPKEY devpkey_pdoname = DEVPKEY_Device_PDOName();
                return GetDeviceProperty(devInfo, ref devInfoData, devpkey_pdoname);
            } finally {
                SetupDiDestroyDeviceInfoList(devInfo);
            }
        }

        public static bool IsAdministrator() {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static bool Is64BitOperatingSystem() {
            if (IntPtr.Size == 8) {
                return true;
            }
            IntPtr kernel32 = GetModuleHandleW("kernel32.dll");
            if (kernel32 == IntPtr.Zero) {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "GetModuleHandleW " + err.ToString());
            }
            IntPtr ptrIsWow64Process = GetProcAddress(kernel32, "IsWow64Process");
            if (ptrIsWow64Process == IntPtr.Zero) {
                return false;
            }
            IsWow64ProcessDelegate fnIsWow64Process = (IsWow64ProcessDelegate)Marshal.GetDelegateForFunctionPointer(ptrIsWow64Process, typeof(IsWow64ProcessDelegate));
            bool wow64;
            if (fnIsWow64Process(GetCurrentProcess(), out wow64) && wow64) {
                return true;
            }
            return false;
        }
    }
}
"@
Write-Verbose "Loading helper types"
Add-Type -TypeDefinition $TypeDefinition -ErrorAction SilentlyContinue | Out-Null

# downlevel admin detection for old PowerShell without Requires statement support
if (![XSA468Workaround.NativeMethods]::IsAdministrator()) {
    throw "This script requires Administrator privileges!"
}

if ([System.IntPtr]::Size -ne 8 -and [XSA468Workaround.NativeMethods]::Is64BitOperatingSystem()) {
    throw "Cannot run this script from PowerShell x86 on 64-bit OS!"
}

# SDDL_DEVOBJ_SYS_ALL_ADM_ALL
$Script:DeviceSdBytes = @(
    1, 0, 4, 144, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0,
    20, 0, 0, 0, 2, 0, 52, 0,
    2, 0, 0, 0, 0, 0, 20, 0,
    0, 0, 0, 16, 1, 1, 0, 0,
    0, 0, 0, 5, 18, 0, 0, 0,
    0, 0, 24, 0, 0, 0, 0, 16,
    1, 2, 0, 0, 0, 0, 0, 5,
    32, 0, 0, 0, 32, 2, 0, 0
)  # "D:P(A;;GA;;;SY)(A;;GA;;;BA)"

$Script:WmiSdBytes = @(
    1, 0, 4, 128, 20, 0, 0, 0,
    36, 0, 0, 0, 0, 0, 0, 0,
    52, 0, 0, 0, 1, 2, 0, 0,
    0, 0, 0, 5, 32, 0, 0, 0,
    32, 2, 0, 0, 1, 2, 0, 0,
    0, 0, 0, 5, 32, 0, 0, 0,
    32, 2, 0, 0, 2, 0, 52, 0,
    2, 0, 0, 0, 0, 0, 20, 0,
    0, 0, 0, 16, 1, 1, 0, 0,
    0, 0, 0, 5, 18, 0, 0, 0,
    0, 0, 24, 0, 0, 0, 0, 16,
    1, 2, 0, 0, 0, 0, 0, 5,
    32, 0, 0, 0, 32, 2, 0, 0
)  # "O:BAG:BAD:(A;;GA;;;BA)(A;;GA;;;SY)"
$Script:WmiSecurityKey = "HKLM:\SYSTEM\CurrentControlSet\Control\WMI\Security"

$Script:ScheduledTaskName = "XSA468Workaround"
$Script:InstallPath = "$env:ProgramFiles\XSA468Workaround.ps1"
$Script:PowershellPath = "$Env:windir\System32\WindowsPowerShell\v1.0\powershell.exe"

# Prepackaged arguments for each device type
$Script:DeviceTypes = @(
    @{
        Class               = "System"
        CompatibleIdType    = "xenclass"
        CompatibleIdPattern = '*&DEV_IFACE&*'
    }
)

$Script:WmiGuids = @(
    "1D80EB99-A1D6-4492-B62F-8B4549FF0B5E"
    "12138A69-97B2-49DD-B9DE-54749AABC789"
    "AB8136BF-8EA7-420D-ADAD-89C83E587925"
)

# Only list SIDs that belong to the default insecure configuration
$Script:VulnerableSids = @(
    (New-Object `
        -TypeName System.Security.Principal.SecurityIdentifier `
        -ArgumentList ([System.Security.Principal.WellKnownSidType]::WorldSid), $null),
    (New-Object `
        -TypeName System.Security.Principal.SecurityIdentifier `
        -ArgumentList ([System.Security.Principal.WellKnownSidType]::RestrictedCodeSid), $null)
)

if ($PSVersionTable.PSVersion.Major -lt 3) {
    $Script:MyPSCommandPath = $MyInvocation.MyCommand.Path
}
else {
    $Script:MyPSCommandPath = $PSCommandPath
}

function Get-XenDevice {
    param ()

    Get-WmiObject -Query "select * from Win32_PnPEntity where ConfigManagerErrorCode=0 and DeviceID like '%&DEV_IFACE%'"
}

function Get-XenDevicePdoPath {
    param ($DeviceID)

    Write-Verbose "Getting PDO path for $DeviceID"
    $pdoName = [XSA468Workaround.NativeMethods]::GetDevicePdoName($DeviceID)
    if (!$pdoName) {
        throw "Cannot find PDO path of $DeviceID"
    }
    return "\\.\GLOBALROOT" + $pdoName
}

function Set-XenDriverSecurity {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param (
        [Parameter(Mandatory = $true)][byte[]]$SecurityDescriptorBytes
    )

    Get-XenDevice | ForEach-Object {
        $deviceId = $_.PNPDeviceID
        Write-Verbose "Securing $deviceId"
        $regpath = Join-Path HKLM:\SYSTEM\CurrentControlSet\Enum $deviceId
        Write-Verbose "regpath: $regpath"
        if ($PSCmdlet.ShouldProcess($regpath, "Set security")) {
            if ($null -eq (Get-ItemProperty $regpath -Name Security -ErrorAction SilentlyContinue)) {
                Set-ItemProperty $regpath -Name Security -Value $SecurityDescriptorBytes -WhatIf:$WhatIfPreference
            }
            else {
                Write-Verbose "Device $deviceId already has Security value, skipping"
            }
        }
    }
}

function Set-XenWmiSecurity {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param (
        [Parameter(Mandatory = $true)][string]$WmiGuid,
        [Parameter(Mandatory = $true)][byte[]]$SecurityDescriptorBytes
    )

    if ($PSCmdlet.ShouldProcess($WmiGuid, "Set security")) {
        if ($null -eq (Get-ItemProperty -Path $Script:WmiSecurityKey -Name $WmiGuid -ErrorAction SilentlyContinue)) {
            Set-ItemProperty -Path $Script:WmiSecurityKey -Name $WmiGuid -Type Binary -Value $SecurityDescriptorBytes -WhatIf:$WhatIfPreference
        }
        else {
            Write-Verbose "WMI GUID $WmiGuid already has Security value, skipping"
        }
    }
}

function Protect-XenDeviceObject {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param (
        [Parameter(Mandatory = $true)][byte[]]$SecurityDescriptorBytes
    )

    Get-XenDevice | ForEach-Object {
        $deviceId = $_.PNPDeviceID
        $devicePath = Get-XenDevicePdoPath -DeviceId $deviceId
        Write-Verbose "devicePath $devicePath"
        Write-Verbose "Protecting $deviceId, devicePath = $devicePath"

        # FileInfo.SetAccessControl explodes when it encounters a device object, so we have to use P/Invoke
        if ($PSCmdlet.ShouldProcess($devicePath, "Set DACL")) {
            [XSA468Workaround.NativeMethods]::SetObjectDacl($devicePath, $SecurityDescriptorBytes)
            Write-Verbose "Successfully set DACL on $devicePath"
        }
    }
}

function Test-XenDeviceObject {
    [CmdletBinding()]
    param ()

    $foundObjects = @()
    Get-XenDevice | ForEach-Object {
        $deviceId = $_.PNPDeviceID
        $devicePath = Get-XenDevicePdoPath -DeviceId $deviceId
        Write-Verbose "devicePath $devicePath"
        Write-Verbose "Testing $deviceId, devicePath = $devicePath"

        # on Server 2016 Get-Acl doesn't work on device objects, so we again have to use P/Invoke
        $sdBytes = [XSA468Workaround.NativeMethods]::GetObjectDacl($devicePath)
        $sd = New-Object -TypeName System.Security.AccessControl.RawSecurityDescriptor -ArgumentList $sdBytes, 0
        Write-Verbose ($sd.GetSddlForm([System.Security.AccessControl.AccessControlSections]::All))
        $sd.DiscretionaryAcl | Where-Object { $_.AceType -eq "AccessAllowed" } | ForEach-Object {
            $subj = $_.SecurityIdentifier
            $foundObjects += @(
                [PSCustomObject]@{
                    DeviceId   = $deviceId
                    Vulnerable = !!($Script:VulnerableSids | Where-Object { $_ -eq $subj })
                })
        }
    }
    return $foundObjects
}

if ($Scan) {
    $Script:FoundDevice = $false
    $Script:IsVulnerable = $false

    Write-Host
    Write-Host "Looking for vulnerable XenIface objects"
    $objects = Test-XenDeviceObject
    if ($objects) {
        $Script:FoundDevice = $true
    }
    $objects | Where-Object { $_.Vulnerable } | ForEach-Object {
        Write-Host "Found vulnerable object $($_.DeviceID)"
        $Script:IsVulnerable = $true
    }

    if ($Script:FoundDevice) {
        Write-Host
        Write-Host "Looking for vulnerable XenIface WMI GUIDs"
        foreach ($wmiGuid in $Script:WmiGuids) {
            Write-Verbose "Testing WMI GUID $wmiGuid"
            if ($null -eq (Get-ItemProperty -Path $Script:WmiSecurityKey -Name $wmiGuid -ErrorAction SilentlyContinue)) {
                Write-Host "Found vulnerable WMI GUID $wmiGuid"
                $Script:IsVulnerable = $true
            }
        }
    }

    Write-Host
    if (!$Script:FoundDevice) {
        Write-Host "Did not detect XenIface"
    }
    elseif ($Script:IsVulnerable) {
        Write-Host "Found XenIface vulnerability, it's recommended to run the script"
    }
    else {
        Write-Host "Did not find evidence of XenIface vulnerability"
    }
    Write-Output $Script:IsVulnerable
}

elseif ($Invoke) {
    if (!$NoSecureObjects) {
        Write-Host
        Write-Host "Protecting active Xen device objects"
        try {
            Protect-XenDeviceObject -SecurityDescriptorBytes $Script:DeviceSdBytes -WhatIf:$WhatIfPreference
        }
        catch {
            Write-Error $_
        }
    }

    if (!$NoSetRegistry) {
        Write-Host
        Write-Host "Setting Xen device security registry values"
        try {
            Set-XenDriverSecurity -SecurityDescriptorBytes $Script:DeviceSdBytes -WhatIf:$WhatIfPreference
        }
        catch {
            Write-Error $_
        }

        Write-Host
        Write-Host "Setting WMI security registry values"
        foreach ($wmiGuid in $Script:WmiGuids) {
            try {
                Set-XenWmiSecurity -WmiGuid $wmiGuid -SecurityDescriptorBytes $Script:WmiSdBytes -WhatIf:$WhatIfPreference
            }
            catch {
                Write-Error $_
            }
        }
    }
}

elseif ($PSCmdlet.ParameterSetName -ieq "Install") {
    Write-Verbose "Current path: $Script:MyPSCommandPath"
    Write-Verbose "Install path: $Script:InstallPath"

    Write-Host
    Write-Host "Running script as SYSTEM"
    if ((Convert-Path $Script:MyPSCommandPath -ErrorAction SilentlyContinue) -ieq `
        (Convert-Path $Script:InstallPath -ErrorAction SilentlyContinue)) {
        Write-Host "Cannot install from already-installed script, abandoning"
    }
    else {
        # copy the script to a secure location for the SYSTEM task
        Copy-Item $Script:MyPSCommandPath -Destination $Script:InstallPath -Force -WhatIf:$WhatIfPreference
        if ($PSCmdlet.ShouldProcess($Script:ScheduledTaskName, "Delete old task")) {
            Write-Host "Deleting old task (please ignore next error)"
            & "$Env:windir\system32\schtasks.exe" /delete /tn $Script:ScheduledTaskName /f
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
        $command = "& '$Script:InstallPath' $($cmdArgs -join ' ')"
        $encodedCommand = [System.Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($command))
        $argString = "\""$Script:PowershellPath\"" -NoProfile -NonInteractive -EncodedCommand $encodedCommand"
        Write-Verbose "Task executable: $Script:PowershellPath"
        Write-Verbose "Task arguments: $argString"

        if ($PSCmdlet.ShouldProcess($Script:ScheduledTaskName, "Create scheduled task")) {
            Write-Host "Starting mitigation task"
            & "$Env:windir\system32\schtasks.exe" /create /f /ru SYSTEM /rl highest /sc onstart /tr $argString /tn $Script:ScheduledTaskName
            if ($LASTEXITCODE -ne 0) {
                throw "schtasks.exe error $LASTEXITCODE"
            }
            & "$Env:windir\system32\schtasks.exe" /run /tn $Script:ScheduledTaskName
            if ($LASTEXITCODE -ne 0) {
                throw "schtasks.exe error $LASTEXITCODE"
            }

            Write-Host "Waiting for task to finish"
            # wait for a max of 5 minutes before cleaning up
            for ($i = 0; $i -lt 60; $i++) {
                Start-Sleep -Seconds 5
                $result = (& "$Env:windir\system32\schtasks.exe" /query /tn $Script:ScheduledTaskName /fo list | Select-String "Running")
                if ($LASTEXITCODE -ne 0) {
                    throw "schtasks.exe error $LASTEXITCODE"
                }
                if (!$result) {
                    break
                }
            }
            Write-Host "Getting task status"
            & "$Env:windir\system32\schtasks.exe" /query /tn $Script:ScheduledTaskName /fo list /v | Select-String "Last Result:"
            if ($LASTEXITCODE -ne 0) {
                throw "schtasks.exe error $LASTEXITCODE"
            }

            Write-Host "Cleaning up"
            & "$Env:windir\system32\schtasks.exe" /delete /tn $Script:ScheduledTaskName /f
            if ($LASTEXITCODE -ne 0) {
                throw "schtasks.exe error $LASTEXITCODE"
            }
            Remove-Item $Script:InstallPath -Force -ErrorAction Continue
        }
    }
}
