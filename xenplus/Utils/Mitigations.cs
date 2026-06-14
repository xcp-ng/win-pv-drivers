using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.System.LibraryLoader;
using Windows.Win32.System.SystemServices;
using Windows.Win32.System.Threading;

// abuse Mitigations.cs as a place for this as it's part of the mitigations
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace XenPlus;

// #define GET_MITIGATION_POLICY_BEFORE_SETTING

sealed class Mitigations(EarlyLogger logger) {
    readonly SafeHandle CurrentProcess = new CurrentProcessSafeHandle();

    // Convenience loggers

#if GET_MITIGATION_POLICY_BEFORE_SETTING
    void LogMitigationQueryError([CallerMemberName] string memberName = "") {
        logger.LogWarning($"Cannot query {memberName} mitigation (error {Marshal.GetLastPInvokeError()})");
    }
#endif

    void LogMitigationEnableError([CallerMemberName] string memberName = "") {
        logger.LogWarning($"Cannot enable {memberName} mitigation (error {Marshal.GetLastPInvokeError()})");
    }

    void EnableDllProtection() {
        if (!PInvoke.SetDefaultDllDirectories(LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_SEARCH_SYSTEM32)) {
            LogMitigationEnableError();
        }
    }

    void EnableStrictHandleChecks() {
        var policy = new PROCESS_MITIGATION_STRICT_HANDLE_CHECK_POLICY();
        var policyBuf = MemoryMarshal.AsBytes(new Span<PROCESS_MITIGATION_STRICT_HANDLE_CHECK_POLICY>(ref policy));

#if GET_MITIGATION_POLICY_BEFORE_SETTING
            if (!PInvoke.GetProcessMitigationPolicy(
                CurrentProcess,
                PROCESS_MITIGATION_POLICY.ProcessStrictHandleCheckPolicy,
                policyBuf)) {
                LogMitigationQueryError();
                return;
            }
#endif
        policy.Anonymous.Anonymous.RaiseExceptionOnInvalidHandleReference = true;
        policy.Anonymous.Anonymous.HandleExceptionsPermanentlyEnabled = true;
        if (!PInvoke.SetProcessMitigationPolicy(
            PROCESS_MITIGATION_POLICY.ProcessStrictHandleCheckPolicy,
            policyBuf)) {
            LogMitigationEnableError();
        }
    }

    // not sure if this is actually windows8.0 but MS docs say so...
    void EnableRedirectionGuard() {
        var policy = new PROCESS_MITIGATION_REDIRECTION_TRUST_POLICY();
        var policyBuf = MemoryMarshal.AsBytes(new Span<PROCESS_MITIGATION_REDIRECTION_TRUST_POLICY>(ref policy));

#if GET_MITIGATION_POLICY_BEFORE_SETTING
        if (!PInvoke.GetProcessMitigationPolicy(
            CurrentProcess,
            PROCESS_MITIGATION_POLICY.ProcessRedirectionTrustPolicy,
            policyBuf)) {
            LogMitigationQueryError();
            return;
        }
#endif
        policy.Anonymous.Anonymous.EnforceRedirectionTrust = true;
        if (!PInvoke.SetProcessMitigationPolicy(
            PROCESS_MITIGATION_POLICY.ProcessRedirectionTrustPolicy,
            policyBuf)) {
            LogMitigationEnableError();
        }
    }

    void DisableSystemCalls() {
        var policy = new PROCESS_MITIGATION_SYSTEM_CALL_DISABLE_POLICY();
        var policyBuf = MemoryMarshal.AsBytes(new Span<PROCESS_MITIGATION_SYSTEM_CALL_DISABLE_POLICY>(ref policy));

#if GET_MITIGATION_POLICY_BEFORE_SETTING
        if (!PInvoke.GetProcessMitigationPolicy(
            CurrentProcess,
            PROCESS_MITIGATION_POLICY.ProcessSystemCallDisablePolicy,
            policyBuf)) {
            LogMitigationQueryError();
            return;
        }
#endif
        policy.Anonymous.Anonymous.DisallowWin32kSystemCalls = true;
        policy.Anonymous.Anonymous.AuditDisallowWin32kSystemCalls = true;
        if (!PInvoke.SetProcessMitigationPolicy(
            PROCESS_MITIGATION_POLICY.ProcessSystemCallDisablePolicy,
            policyBuf)) {
            LogMitigationEnableError();
        }
    }

    void DisableExtensionPoints() {
        var policy = new PROCESS_MITIGATION_EXTENSION_POINT_DISABLE_POLICY();
        var policyBuf = MemoryMarshal.AsBytes(new Span<PROCESS_MITIGATION_EXTENSION_POINT_DISABLE_POLICY>(ref policy));

#if GET_MITIGATION_POLICY_BEFORE_SETTING
        if (!PInvoke.GetProcessMitigationPolicy(
            CurrentProcess,
            PROCESS_MITIGATION_POLICY.ProcessExtensionPointDisablePolicy,
            policyBuf)) {
            LogMitigationQueryError();
            return;
        }
#endif
        policy.Anonymous.Anonymous.DisableExtensionPoints = true;
        if (!PInvoke.SetProcessMitigationPolicy(
            PROCESS_MITIGATION_POLICY.ProcessExtensionPointDisablePolicy,
            policyBuf)) {
            LogMitigationEnableError();
        }
    }

    [SupportedOSPlatform("windows10.0.14393")]
    void EnableImageLoadPolicies() {
        var policy = new PROCESS_MITIGATION_IMAGE_LOAD_POLICY();
        var policyBuf = MemoryMarshal.AsBytes(new Span<PROCESS_MITIGATION_IMAGE_LOAD_POLICY>(ref policy));

#if GET_MITIGATION_POLICY_BEFORE_SETTING
        if (!PInvoke.GetProcessMitigationPolicy(
            CurrentProcess,
            PROCESS_MITIGATION_POLICY.ProcessImageLoadPolicy,
            policyBuf)) {
            LogMitigationQueryError();
            return;
        }
#endif
        policy.Anonymous.Anonymous.NoRemoteImages = true;
        policy.Anonymous.Anonymous.NoLowMandatoryLabelImages = true;
        policy.Anonymous.Anonymous.PreferSystem32Images = true;
        if (!PInvoke.SetProcessMitigationPolicy(
            PROCESS_MITIGATION_POLICY.ProcessImageLoadPolicy,
            policyBuf)) {
            LogMitigationEnableError();
        }
    }

    [SupportedOSPlatform("windows10.0.14393")]
    void EnableDynamicCodePolicies() {
        /*
         * Known issue:
         * Application: xenplus.exe
         * CoreCLR Version: 10.0.8
         * Description: The process was terminated due to an unhandled exception.
         * Exception Info: System.PlatformNotSupportedException: Dynamic entrypoint allocation is not supported in the
         * current environment.
         *    at System.Runtime.ThunkBlocks.GetNewThunksBlock() + 0x1e8
         *    at System.Runtime.ThunksHeap..ctor(IntPtr) + 0x31
         *    at System.Runtime.InteropServices.PInvokeMarshal.AllocateThunk(Delegate) + 0x3a
         *    at System.Runtime.CompilerServices.ConditionalWeakTable`2.GetOrAdd(TKey, Func`2) + 0x41
         *    at System.Runtime.InteropServices.PInvokeMarshal.GetFunctionPointerForDelegate(Delegate) + 0xc5
         *    at System.ServiceProcess.ServiceBase.GetEntry() + 0x21
         *    at System.ServiceProcess.ServiceBase.Run(ServiceBase[]) + 0xbc
         *    at Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceLifetime.Run() + 0x14
         * --- End of stack trace from previous location ---
         *    at Microsoft.Extensions.Hosting.Internal.Host.<StartAsync>d__14.MoveNext() + 0x3ec
         * --- End of stack trace from previous location ---
         *    at Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.<RunAsync>d__4.MoveNext() + 0xda
         * --- End of stack trace from previous location ---
         *    at Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.<RunAsync>d__4.MoveNext() + 0x31a
         * --- End of stack trace from previous location ---
         *    at XenPlus.Program.Main(String[] args) + 0x25b
         */

        var policy = new PROCESS_MITIGATION_DYNAMIC_CODE_POLICY();
        var policyBuf = MemoryMarshal.AsBytes(new Span<PROCESS_MITIGATION_DYNAMIC_CODE_POLICY>(ref policy));

#if GET_MITIGATION_POLICY_BEFORE_SETTING
        if (!PInvoke.GetProcessMitigationPolicy(
            CurrentProcess,
            PROCESS_MITIGATION_POLICY.ProcessDynamicCodePolicy,
            policyBuf)) {
            LogMitigationQueryError();
            return;
        }
#endif
        policy.Anonymous.Anonymous.ProhibitDynamicCode = false;
        policy.Anonymous.Anonymous.AuditProhibitDynamicCode = true;
        if (!PInvoke.SetProcessMitigationPolicy(
            PROCESS_MITIGATION_POLICY.ProcessDynamicCodePolicy,
            policyBuf)) {
            LogMitigationEnableError();
        }
    }

    public void EnableAll() {
        EnableDllProtection();
        EnableStrictHandleChecks();
        EnableRedirectionGuard();
        //DisableSystemCalls();
        DisableExtensionPoints();
        EnableImageLoadPolicies();
        //EnableDynamicCodePolicies();
    }
}
