using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.System.Wmi;

namespace XenPlus;

sealed class WmiService {
    const uint RPC_C_AUTHN_WINNT = 10;
    const uint RPC_C_AUTHZ_NONE = 0;

    readonly IWbemServices _services;

    static readonly Lazy<bool> InitializeSecurity = new(() => {
        unsafe {
            PInvoke.CoInitializeSecurity(
                new(),
                -1,
                null,
                RPC_C_AUTHN_LEVEL.RPC_C_AUTHN_LEVEL_DEFAULT,
                RPC_C_IMP_LEVEL.RPC_C_IMP_LEVEL_IMPERSONATE,
                null,
                EOLE_AUTHENTICATION_CAPABILITIES.EOAC_NONE).ThrowOnFailure();
        }
        return true;
    });

    static SysFreeStringSafeHandle StringToBSTR(string value) {
        return new SysFreeStringSafeHandle(Marshal.StringToBSTR(value), true);
    }

    public WmiService(string wmiNamespace) {
        _ = InitializeSecurity.Value;

        IWbemLocator locator = WbemLocator.CreateInstance<IWbemLocator>();

        using var ns = StringToBSTR(wmiNamespace);
        locator.ConnectServer(
            ns,
            new SysFreeStringSafeHandle(),
            new SysFreeStringSafeHandle(),
            new SysFreeStringSafeHandle(),
            0,
            new SafeFileHandle(),
            null,
            out _services);

        unsafe {
            PInvoke.CoSetProxyBlanket(
                _services,
                RPC_C_AUTHN_WINNT,
                RPC_C_AUTHZ_NONE,
                null,
                RPC_C_AUTHN_LEVEL.RPC_C_AUTHN_LEVEL_CALL,
                RPC_C_IMP_LEVEL.RPC_C_IMP_LEVEL_IMPERSONATE,
                null,
                EOLE_AUTHENTICATION_CAPABILITIES.EOAC_NONE).ThrowOnFailure();
        }
    }

    internal IEnumerable<IWbemClassObject> ExecQuery(string query) {
        using var wql = StringToBSTR("WQL");
        using var queryHandle = StringToBSTR(query);

        _services.ExecQuery(
            wql,
            queryHandle,
            WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_FORWARD_ONLY | WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_RETURN_IMMEDIATELY,
            null,
            out var enumerator);

        var obj = new IWbemClassObject?[1];
        while (true) {
            enumerator.Next(
                PInvoke.WBEM_INFINITE,
                obj,
                out var returned).ThrowOnFailure();
            for (uint i = 0; i < returned; i++) {
                yield return obj[i]!;
            }
            if (returned == 0) {
                yield break;
            }
            obj[0] = null;
        }

    }

    internal IWbemCallResult ExecMethod(IWbemClassObject obj, string methodName) {
        obj.GetObjectText(0, out var objectPath);
        using (objectPath) {
            using var methodHandle = StringToBSTR(methodName);
            IWbemCallResult? result = null;

            _services.ExecMethod(
                objectPath,
                methodHandle,
                0,
                null,
                null,
                ref Unsafe.NullRef<IWbemClassObject>(),
                ref result);

            return result;
        }
    }
}
