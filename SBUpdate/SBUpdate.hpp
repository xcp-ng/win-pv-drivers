#pragma once

#include <cstdio>
#include <system_error>
#include <algorithm>
#include <array>
#include <vector>
#include <atlbase.h>
#include <atlfile.h>
#include <atlsecurity.h>
#include <comdef.h>
#include <WbemIdl.h>
#include <Windows.h>

// Windows headers don't provide definitions for these attributes, so use our own
static constexpr DWORD SBUPDATE_VARIABLE_ATTRIBUTE_NON_VOLATILE = 0x00000001;
static constexpr DWORD SBUPDATE_VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS = 0x00000002;
static constexpr DWORD SBUPDATE_VARIABLE_ATTRIBUTE_RUNTIME_ACCESS = 0x00000004;
static constexpr DWORD SBUPDATE_VARIABLE_ATTRIBUTE_HARDWARE_ERROR_RECORD = 0x00000008;
static constexpr DWORD SBUPDATE_VARIABLE_ATTRIBUTE_AUTHENTICATED_WRITE_ACCESS = 0x00000010;
static constexpr DWORD SBUPDATE_VARIABLE_ATTRIBUTE_TIME_BASED_AUTHENTICATED_WRITE_ACCESS = 0x00000020;
static constexpr DWORD SBUPDATE_VARIABLE_ATTRIBUTE_APPEND_WRITE = 0x00000040;

struct SecureBootVariable {
    const wchar_t* VariableName;
    const wchar_t* VariableGuid;
    const DWORD VariableAttributes;
};

// win32_encryptablevolume.mof

static constexpr const wchar_t* FveNamespace = L"Root\\CIMV2\\Security\\MicrosoftVolumeEncryption";
static constexpr const wchar_t* FveGetSecureBootBindingState = L"GetSecureBootBindingState";
static constexpr const wchar_t* FveDisableKeyProtectors = L"DisableKeyProtectors";

enum FveSecureBootBindingState {
    FveSecureBootBindingStateNotPossible,
    FveSecureBootBindingStateDisabledByPolicy,
    FveSecureBootBindingStatePossible,
    FveSecureBootBindingStateBound,
};
