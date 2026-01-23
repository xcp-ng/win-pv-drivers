[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "Low")]
param()

$ErrorActionPreference = "Stop"
$Vendor = "XCP-ng"
$XenTools = "$PSScriptRoot\XenTools-x64.msi"

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
        # cleaning succeeded, must reboot
        Restart-Computer -Force -WhatIf:$WhatIfPreference
    }
    64 {
        # ready for onboarding, we can install now
        if (!$WhatIfPreference) {
            $msiexec = Start-Process `
                -Wait "$env:windir\system32\msiexec.exe" `
                -ArgumentList "/i", $XenTools, "/passive", "/norestart", "/log", "$env:TEMP\xentools-onboard.log" `
                -PassThru
            if ($msiexec.ExitCode -ne 0) {
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
