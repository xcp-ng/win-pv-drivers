#include "SBUpdate.hpp"

#ifndef UNICODE
#error "SBUpdate assumes Unicode!"
#endif

static const std::array<SecureBootVariable, 3> AllowedVariables = {
    SecureBootVariable{
        .VariableName = L"KEK",
        .VariableGuid = L"{8be4df61-93ca-11d2-aa0d-00e098032b8c}",
        .VariableAttributes = SBUPDATE_VARIABLE_ATTRIBUTE_NON_VOLATILE |
            SBUPDATE_VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_RUNTIME_ACCESS |
            SBUPDATE_VARIABLE_ATTRIBUTE_TIME_BASED_AUTHENTICATED_WRITE_ACCESS,
    },
    SecureBootVariable{
        .VariableName = L"db",
        .VariableGuid = L"{d719b2cb-3d3a-4596-a3bc-dad00e67656f}",
        .VariableAttributes = SBUPDATE_VARIABLE_ATTRIBUTE_NON_VOLATILE |
            SBUPDATE_VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_RUNTIME_ACCESS |
            SBUPDATE_VARIABLE_ATTRIBUTE_TIME_BASED_AUTHENTICATED_WRITE_ACCESS,
    },
    SecureBootVariable{
        .VariableName = L"dbx",
        .VariableGuid = L"{d719b2cb-3d3a-4596-a3bc-dad00e67656f}",
        .VariableAttributes = SBUPDATE_VARIABLE_ATTRIBUTE_NON_VOLATILE |
            SBUPDATE_VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_RUNTIME_ACCESS |
            SBUPDATE_VARIABLE_ATTRIBUTE_TIME_BASED_AUTHENTICATED_WRITE_ACCESS,
    },
};

static void PrintUsage(_In_ const wchar_t *name) {
    wprintf(L"Usage: %s [--append] <variable name> <update file path>\n", name);
}

static void EnablePrivileges() {
    CAccessToken token;
    if (!token.GetProcessToken(TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES))
        throw std::system_error(GetLastError(), std::system_category(), "GetProcessToken");
    if (!token.EnablePrivilege(SE_SYSTEM_ENVIRONMENT_NAME))
        throw std::system_error(GetLastError(), std::system_category(), "EnablePrivilege(SE_SYSTEM_ENVIRONMENT_NAME)");
}

static void ReadVariableBlob(_In_ const wchar_t *blobpath, _Out_ std::vector<uint8_t> &blob) {
    CAtlFile blobfile;
    HRESULT hr = blobfile.Create(blobpath, GENERIC_READ, FILE_SHARE_READ, OPEN_EXISTING);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "blobfile.Open");
    }

    ULONGLONG blobsize;
    hr = blobfile.GetSize(blobsize);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "blobfile.GetSize");
    }
    if (blobsize > 0x100000) {
        // arbitrary 1MB size limit
        throw std::runtime_error("Input file too big");
    }
    wprintf(L"Input file is %llu bytes\n", blobsize);

    blob.resize(blobsize);
    ULONGLONG pos = 0;
    while (pos < blobsize) {
        DWORD readcount;
        hr = blobfile.Read(&blob[pos], static_cast<DWORD>(blob.size() - pos), readcount);
        if (FAILED(hr)) {
            throw std::system_error(hr, std::system_category(), "blobfile.Read");
        }
        wprintf(L"Read %lu\n", readcount);
        if (!readcount) {
            throw std::runtime_error("Input file truncated");
        }
        pos += readcount;
    }
}

static void DoInitializeCom() {
    auto hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "CoInitializeEx");
    }

    hr = CoInitializeSecurity(
        NULL,
        -1,
        NULL,
        NULL,
        RPC_C_AUTHN_LEVEL_DEFAULT,
        RPC_C_IMP_LEVEL_IMPERSONATE,
        NULL,
        EOAC_NONE,
        NULL);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "CoInitializeSecurity");
    }
}

static void OpenWbemServices(_In_ const wchar_t *resource, _Outptr_ IWbemServices **outServices) {
    CComPtr<IWbemLocator> locator;
    HRESULT hr = locator.CoCreateInstance(CLSID_WbemLocator);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "CoCreateInstance(CLSID_WbemLocator)");
    }

    hr = locator->ConnectServer(_bstr_t(resource), NULL, NULL, NULL, 0, NULL, NULL, outServices);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "ConnectServer");
    }
}

static bool FindFveOsDrive(_In_ IWbemServices *services, _Outptr_ IWbemClassObject **outObject) {
    *outObject = nullptr;

    HRESULT hr = CoSetProxyBlanket(
        services,
        RPC_C_AUTHN_WINNT,
        RPC_C_AUTHZ_NONE,
        NULL,
        RPC_C_AUTHN_LEVEL_CALL,
        RPC_C_IMP_LEVEL_IMPERSONATE,
        NULL,
        EOAC_NONE);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "CoSetProxyBlanket");
    }

    CComPtr<IEnumWbemClassObject> enumerator;
    hr = services->ExecQuery(
        _bstr_t(L"WQL"),
        _bstr_t(L"select * from Win32_EncryptableVolume where VolumeType = 0"),
        WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
        NULL,
        &enumerator);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "services->ExecQuery(Win32_EncryptableVolume)");
    }

    ULONG returned = 0;
    hr = enumerator->Next(WBEM_INFINITE, 1, outObject, &returned);
    if (FAILED(hr)) {
        *outObject = nullptr;
        throw std::system_error(hr, std::system_category(), "Next(Win32_EncryptableVolume)");
    }

    return !!*outObject;
}

static void GetWbemInstancePath(_In_ IWbemClassObject *obj, _Outptr_ BSTR *output) {
    VARIANT instancePath;
    HRESULT hr;

    VariantInit(&instancePath);
    hr = obj->Get(L"__PATH", 0, &instancePath, nullptr, nullptr);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "fveOsDrive->Get(__PATH)");
    }

    if (instancePath.vt != VT_BSTR) {
        throw std::runtime_error("Unknown fveInstancePath type");
    }

    *output = instancePath.bstrVal;
}

static bool FveIsSecureBootBound(
    _In_ IWbemServices *services,
    _In_ IWbemClassObject *win32_EncryptableVolumeClass,
    _In_ BSTR instancePath) {
    HRESULT hr;

    CComPtr<IWbemClassObject> getSecureBootBindingState_Out;
    hr = win32_EncryptableVolumeClass
             ->GetMethod(FveGetSecureBootBindingState, 0, nullptr, &getSecureBootBindingState_Out);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "GetMethod(GetSecureBootBindingState)");
    }

    CComPtr<IWbemClassObject> outParams;
    CComPtr<IWbemCallResult> result;
    hr =
        services
            ->ExecMethod(instancePath, _bstr_t(FveGetSecureBootBindingState), 0, nullptr, nullptr, &outParams, &result);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "ExecMethod(GetSecureBootBindingState)");
    }

    long status;
    hr = result->GetCallStatus(WBEM_INFINITE, &status);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "result->GetCallStatus");
    }
    if (status) {
        throw std::system_error(hr, std::system_category(), "GetSecureBootBindingState");
    }

    CComVariant bindingState;
    hr = outParams->Get(L"BindingState", 0, &bindingState, nullptr, nullptr);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "outParams->Get(BindingState)");
    }

    if (bindingState.vt != VT_I4) {
        throw std::runtime_error("Unknown bindingState type");
    }

    wprintf(L"Binding state %u\n", bindingState.lVal);
    return bindingState.ulVal == FveSecureBootBindingStateBound;
}

static void
FveSuspend(_In_ IWbemServices *services, _In_ IWbemClassObject *win32_EncryptableVolumeClass, BSTR fveInstancePath) {
    HRESULT hr;

    CComPtr<IWbemClassObject> disableKeyProtectors_In;
    hr = win32_EncryptableVolumeClass->GetMethod(FveDisableKeyProtectors, 0, &disableKeyProtectors_In, nullptr);
    if (FAILED(hr)) {
        throw std::system_error(
            hr,
            std::system_category(),
            "win32_EncryptableVolumeClass->GetMethod(DisableKeyProtectors)");
    }

    CComPtr<IWbemCallResult> result;
    hr = services->ExecMethod(
        fveInstancePath,
        _bstr_t(FveDisableKeyProtectors),
        0,
        nullptr,
        disableKeyProtectors_In,
        nullptr,
        &result);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "ExecMethod(DisableKeyProtectors)");
    }

    long status;
    hr = result->GetCallStatus(WBEM_INFINITE, &status);
    if (FAILED(hr)) {
        throw std::system_error(hr, std::system_category(), "result->GetCallStatus");
    }
    if (status) {
        throw std::system_error(hr, std::system_category(), "DisableKeyProtectors");
    }
}

int wmain(int argc, wchar_t **argv) {
    wchar_t *varname = nullptr;
    wchar_t *blobpath = nullptr;
    bool append = false;
    bool noFveSuspend = false;
    HRESULT hr;

    for (int i = 1; i < argc; i++) {
        if (CompareStringOrdinal(L"--append", -1, argv[i], -1, TRUE) == CSTR_EQUAL) {
            append = true;
        } else if (CompareStringOrdinal(L"--no-fve-suspend", -1, argv[i], -1, TRUE) == CSTR_EQUAL) {
            noFveSuspend = true;
        } else if (!varname) {
            varname = argv[i];
        } else if (!blobpath) {
            blobpath = argv[i];
        } else {
            goto help;
        }
    }

    if (!varname || !blobpath) {
        goto help;
    }

    try {
        SetFirmwareEnvironmentVariableExW(L"", L"{00000000-0000-0000-0000-000000000000}", NULL, 0, 0);
        if (GetLastError() == ERROR_INVALID_FUNCTION) {
            throw std::runtime_error("Not running on UEFI system");
        }

        DoInitializeCom();

        bool hasBitlocker = true, isBound = false;

        if (noFveSuspend) {
            wprintf(L"--no-fve-suspend specified, not checking BitLocker\n");
            hasBitlocker = false;
        }

        if (hasBitlocker) {
            CRegKey winPeKey;
            if (winPeKey.Open(HKEY_LOCAL_MACHINE, L"SYSTEM\\CurrentControlSet\\Control\\MiniNT", KEY_READ) ==
                ERROR_SUCCESS) {
                wprintf(L"Running in Windows PE, not checking BitLocker\n");
                hasBitlocker = false;
            }
        }

        CComPtr<IWbemServices> services;
        CComPtr<IWbemClassObject> win32_EncryptableVolumeClass;
        CComBSTR fveInstancePath;
        if (hasBitlocker) {
            try {
                OpenWbemServices(FveNamespace, &services);
            } catch (...) {
                wprintf(L"Cannot connect to BitLocker services. Do you have the BitLocker feature installed?\n");
                wprintf(
                    L"If you're sure you don't have BitLocker enabled, pass --no-fve-suspend to skip BitLocker "
                    L"deactivation.\n");
                throw;
            }

            // get a reference to the Win32_EncryptableVolume class object so that we could get method references
            hr =
                services->GetObjectW(_bstr_t(L"Win32_EncryptableVolume"), 0, NULL, &win32_EncryptableVolumeClass, NULL);

            CComPtr<IWbemClassObject> fveOsDrive;
            FindFveOsDrive(services, &fveOsDrive);
            if (fveOsDrive) {
                wprintf(L"Found active BitLocker OS volume\n");

                // describe the obtained Win32_EncryptableVolume instance object
                CComBSTR fveOsDriveText;
                auto hr = fveOsDrive->GetObjectText(0, &fveOsDriveText);
                if (FAILED(hr)) {
                    throw std::system_error(hr, std::system_category(), "fveOsDrive->GetObjectText");
                }
                wprintf(L"%s\n", static_cast<wchar_t *>(fveOsDriveText));

                GetWbemInstancePath(fveOsDrive, &fveInstancePath);
                isBound = FveIsSecureBootBound(services, win32_EncryptableVolumeClass, fveInstancePath);
                if (isBound) {
                    wprintf(L"BitLocker is Secure Boot-bound, needs rebinding\n");
                }
            }
        }

        auto it = std::find_if(AllowedVariables.begin(), AllowedVariables.end(), [=](const auto &x) {
            return !wcscmp(varname, x.VariableName);
        });
        if (it == AllowedVariables.end()) {
            throw std::runtime_error("Invalid variable");
        }
        wprintf(L"Variable %s %s attr 0x%x\n", it->VariableName, it->VariableGuid, it->VariableAttributes);

        std::vector<uint8_t> blob;
        ReadVariableBlob(blobpath, blob);

        wprintf(L"Enabling privileges\n");
        EnablePrivileges();

        auto attr = it->VariableAttributes;
        if (append) {
            wprintf(L"Append write\n");
            attr |= SBUPDATE_VARIABLE_ATTRIBUTE_APPEND_WRITE;
            // TODO: check if append write is redundant
        }
        if (!SetFirmwareEnvironmentVariableExW(
                it->VariableName,
                it->VariableGuid,
                blob.data(),
                static_cast<DWORD>(blob.size()),
                attr)) {
            throw std::system_error(GetLastError(), std::system_category(), "SetFirmwareEnvironmentVariableExW");
        }

        if (isBound) {
            wprintf(L"Suspending BitLocker\n");
            FveSuspend(services, win32_EncryptableVolumeClass, fveInstancePath);
            wprintf(L"Success! You must reboot now to update BitLocker\n");
        } else {
            wprintf(L"Success!\n");
        }
    } catch (const std::exception &ex) {
        wprintf(L"Error: %S\n", ex.what());
        return 2;
    }

    return 0;

help:
    PrintUsage(argv[0]);
    return 1;
}
