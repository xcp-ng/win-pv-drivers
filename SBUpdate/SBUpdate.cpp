#include <cstdio>
#include <system_error>
#include <algorithm>
#include <memory>
#include <array>
#include <vector>
#include <atlsecurity.h>
#include <Windows.h>

#ifndef UNICODE
#error "SBUpdate assumes Unicode!"
#endif

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

static const std::array<SecureBootVariable, 3> AllowedVariables = {
    SecureBootVariable{
        .VariableName = L"KEK",
        .VariableGuid = L"{8be4df61-93ca-11d2-aa0d-00e098032b8c}",
        .VariableAttributes = SBUPDATE_VARIABLE_ATTRIBUTE_NON_VOLATILE | SBUPDATE_VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_RUNTIME_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_TIME_BASED_AUTHENTICATED_WRITE_ACCESS,
    },
    SecureBootVariable{
        .VariableName = L"db",
        .VariableGuid = L"{d719b2cb-3d3a-4596-a3bc-dad00e67656f}",
        .VariableAttributes = SBUPDATE_VARIABLE_ATTRIBUTE_NON_VOLATILE | SBUPDATE_VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_RUNTIME_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_TIME_BASED_AUTHENTICATED_WRITE_ACCESS,
    },
    SecureBootVariable{
        .VariableName = L"dbx",
        .VariableGuid = L"{d719b2cb-3d3a-4596-a3bc-dad00e67656f}",
        .VariableAttributes = SBUPDATE_VARIABLE_ATTRIBUTE_NON_VOLATILE | SBUPDATE_VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_RUNTIME_ACCESS | SBUPDATE_VARIABLE_ATTRIBUTE_TIME_BASED_AUTHENTICATED_WRITE_ACCESS,
    },
};

static void PrintUsage(const wchar_t* name) {
    wprintf(L"Usage: %s [--append] <variable name> <update file path>\n", name);
}

static std::unique_ptr<FILE, decltype(&fclose)> OpenFile(const wchar_t* path, const wchar_t* mode) {
    FILE* file;
    auto err = _wfopen_s(&file, path, mode);
    if (err) {
        throw std::system_error(err, std::generic_category(), "open(blobpath)");
    }

    return std::unique_ptr<FILE, decltype(&fclose)>(file, fclose);
}

static void EnablePrivileges() {
    CAccessToken token;
    if (!token.GetProcessToken(TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES))
        throw std::system_error(GetLastError(), std::system_category(), "GetProcessToken");
    if (!token.EnablePrivilege(SE_SYSTEM_ENVIRONMENT_NAME))
        throw std::system_error(GetLastError(), std::system_category(), "EnablePrivilege(SE_SYSTEM_ENVIRONMENT_NAME)");
}

int wmain(int argc, wchar_t** argv) {
    wchar_t* varname = nullptr;
    wchar_t* blobpath = nullptr;
    bool append = false;

    for (int i = 1; i < argc; i++) {
        if (CompareStringOrdinal(L"--append", -1, argv[i], -1, TRUE) == CSTR_EQUAL) {
            append = true;
        }
        else if (!varname) {
            varname = argv[i];
        }
        else if (!blobpath) {
            blobpath = argv[i];
        }
        else {
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

        auto it = std::find_if(AllowedVariables.begin(), AllowedVariables.end(), [=](const auto& x) { return !wcscmp(varname, x.VariableName); });
        if (it == AllowedVariables.end()) {
            throw std::runtime_error("Invalid variable");
        }
        wprintf(L"Variable %s %s attr 0x%x\n", it->VariableName, it->VariableGuid, it->VariableAttributes);

        auto blobfile = OpenFile(blobpath, L"rb");

        if (fseek(blobfile.get(), 0, SEEK_END)) {
            throw std::system_error(errno, std::generic_category(), "fseek(blobfile)");
        }

        auto fsize = ftell(blobfile.get());
        if (fsize < 0) {
            throw std::system_error(errno, std::generic_category(), "ftell(blobfile)");
        }
        else if (fsize > 0x100000) {
            // arbitrary 1MB size limit
            throw std::runtime_error("Input file too big");
        }
        wprintf(L"Input file is %d bytes\n", fsize);

        if (fseek(blobfile.get(), 0, SEEK_SET) < 0) {
            throw std::system_error(errno, std::generic_category(), "fseek(blobfile)");
        }

        std::vector<uint8_t> blob(fsize);
        int pos = 0;
        while (pos < fsize) {
            auto readcount = fread_s(&blob[pos], blob.size() - pos, 1, static_cast<size_t>(fsize) - pos, blobfile.get());
            wprintf(L"Read %zu\n", readcount);
            if (ferror(blobfile.get())) {
                throw std::system_error(errno, std::generic_category(), "fread(blobfile)");
            }
            else if (!readcount) {
                throw std::runtime_error("Input file truncated");
            }
            pos += static_cast<int>(readcount);
        }

        wprintf(L"Enabling privileges\n");
        EnablePrivileges();

        auto attr = it->VariableAttributes;
        if (append) {
            wprintf(L"Append write\n");
            attr |= SBUPDATE_VARIABLE_ATTRIBUTE_APPEND_WRITE;
        }
        if (!SetFirmwareEnvironmentVariableExW(it->VariableName, it->VariableGuid, blob.data(), static_cast<DWORD>(blob.size()), attr)) {
            throw std::system_error(GetLastError(), std::system_category(), "SetFirmwareEnvironmentVariableExW");
        }

        wprintf(L"Success!\n");
    }
    catch (const std::exception& ex) {
        wprintf(L"Error: %S\n", ex.what());
        return 2;
    }

    return 0;

help:
    PrintUsage(argv[0]);
    return 1;
}
