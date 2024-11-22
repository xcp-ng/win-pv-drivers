#include <cstdio>
#include <system_error>
#include <array>
#include <vector>
#include <string>
#include <algorithm>
#include <filesystem>
#include <atlsecurity.h>
#include <strsafe.h>
#include <Windows.h>

#include "RegHive.h"

static constexpr auto HiveMountName = L"XenBootFix";

static void PrintUsage(wchar_t* name) {
    wprintf(L"Usage: %s <windir>", name);
}

static std::vector<std::wstring> ParseMultiStrings(_In_reads_(count) const wchar_t* buf, size_t count) {
    std::vector<std::wstring> strings;
    size_t first = 0;
    for (size_t i = 0; i < count; i++) {
        if (buf[i] == 0) {
            strings.emplace_back(buf + first, i - first);
            first = i + 1;
        }
    }
    if (strings.back().empty()) {
        strings.pop_back();
    }
    return strings;
}

static void EnablePrivileges() {
    CAccessToken token;
    if (!token.GetProcessToken(TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES))
        throw std::system_error(GetLastError(), std::system_category(), "GetProcessToken");
    if (!token.EnablePrivilege(SE_BACKUP_NAME))
        throw std::system_error(GetLastError(), std::system_category(), "EnablePrivilege(SE_BACKUP_NAME)");
    if (!token.EnablePrivilege(SE_RESTORE_NAME))
        throw std::system_error(GetLastError(), std::system_category(), "EnablePrivilege(SE_RESTORE_NAME)");
}

static DWORD GetCurrentControlSet(const RegHive& hive) {
    auto keyName = std::wstring(hive.SubKey()) + L"\\Select";

    CRegKey selectKey;
    auto result = selectKey.Open(HKEY_LOCAL_MACHINE, keyName.c_str(), KEY_QUERY_VALUE);
    if (result != ERROR_SUCCESS)
        throw std::system_error(result, std::system_category(), "Couldn't open Select key");

    DWORD value;
    selectKey.QueryDWORDValue(L"Current", value);
    if (value == 0 || value > 999)
        throw std::runtime_error("Unexpected current control set value");

    return value;
}

static void OpenControlSet(const RegHive& hive, CRegKey& key, DWORD controlSet) {
    std::vector<wchar_t> csName(wcslen(hive.SubKey()) + wcslen(L"\\ControlSet000") + 1);
    auto csNameFormat = std::wstring(hive.SubKey()) + L"\\ControlSet%03lu";
    auto hr = StringCchPrintfW(csName.data(), csName.size(), csNameFormat.c_str(), controlSet);
    if (FAILED(hr))
        throw std::system_error(hr, std::system_category(), "StringCchPrintfW");

    auto result = key.Open(hive.HKey(), csName.data(), KEY_ALL_ACCESS);
    if (result != ERROR_SUCCESS)
        throw std::system_error(hr, std::system_category(), "Couldn't open control set key");
}

static const std::array<const wchar_t*, 1> OverridesToDelete = {
    L"stornvme",
};

static void DeleteOverrides(CRegKey& controlSetKey) {
    for (auto overrideName : OverridesToDelete) {
        auto keyName = std::wstring(L"Services\\") + overrideName + L"\\StartOverride";
        wprintf(L"Deleting key \"%s\"\n", keyName.c_str());

        auto result = controlSetKey.RecurseDeleteKey(keyName.c_str());
        if (result != ERROR_SUCCESS)
            wprintf(L"Couldn't delete key \"%s\": 0x%lx\n", keyName.c_str(), result);
    }
}

static const std::array<const wchar_t*, 2> FiltersToRemove = {
    L"xenfilt",
    L"scsifilt",
};

static void RemoveFilter(CRegKey& key, const wchar_t* valueName) {
    ULONG bufsize = 0;

    auto result = key.QueryMultiStringValue(valueName, nullptr, &bufsize);
    if (result != ERROR_SUCCESS)
        return;

    wprintf(L"Cleaning value \"%s\"\n", valueName);

    std::vector<wchar_t> buf(bufsize);
    result = key.QueryMultiStringValue(valueName, buf.data(), &bufsize);
    if (result != ERROR_SUCCESS) {
        wprintf(L"Couldn't get value \"%s\": 0x%lx\n", valueName, result);
        return;
    }

    auto filters = ParseMultiStrings(buf.data(), bufsize);
    wprintf(L"Old value \"%s\": ", valueName);
    for (const auto& filter : filters)
        wprintf(L"\"%s\", ", filter.c_str());
    wprintf(L"\n");

    std::vector<std::wstring> newFilters;
    for (const auto& filter : filters)
        if (std::none_of(
            FiltersToRemove.begin(),
            FiltersToRemove.end(),
            [&](auto& f) { return !_wcsicmp(filter.c_str(), f); }))
            newFilters.emplace_back(filter);

    if (newFilters.empty()) {
        wprintf(L"New value \"%s\": <empty>\n", valueName);

        result = key.DeleteValue(valueName);
        if (result != ERROR_SUCCESS) {
            wprintf(L"Couldn't delete value \"%s\": 0x%lx\n", valueName, result);
            return;
        }
    }
    else {
        wprintf(L"New value \"%s\": ", valueName);
        for (const auto& filter : newFilters)
            wprintf(L"\"%s\", ", filter.c_str());
        wprintf(L"\n");

        bufsize = 1;
        for (const auto& filter : newFilters)
            bufsize += (ULONG)filter.size() + 1;
        buf.clear();
        buf.resize(bufsize, 0);

        wchar_t* ptr = buf.data();
        for (const auto& filter : newFilters) {
            ptr = std::copy(filter.begin(), filter.end(), ptr);
            *ptr++ = 0;
        }
        *ptr++ = 0;

        result = key.SetMultiStringValue(valueName, buf.data());
        if (result != ERROR_SUCCESS) {
            wprintf(L"Couldn't set value \"%s\": 0x%lx\n", valueName, result);
            return;
        }
    }
}

static const std::array<const wchar_t*, 2> FilteredClasses = {
    L"{4d36e96a-e325-11ce-bfc1-08002be10318}", // HDC
    L"{4d36e97d-e325-11ce-bfc1-08002be10318}", // System
};

static const std::array<const wchar_t*, 2> FilterValues = {
    L"LowerFilters",
    L"UpperFilters",
};

static void RemoveFilters(const CRegKey& controlSetKey) {
    for (auto clsid : FilteredClasses) {
        auto keyName = std::wstring(L"Control\\Class\\") + clsid;
        wprintf(L"Cleaning key \"%s\"\n", keyName.c_str());

        CRegKey key;
        auto result = key.Open(controlSetKey, keyName.c_str(), KEY_ALL_ACCESS);
        if (result != ERROR_SUCCESS) {
            wprintf(L"Couldn't open key \"%s\": 0x%lx\n", keyName.c_str(), result);
            continue;
        }

        for (auto valueName : FilterValues) {
            RemoveFilter(key, valueName);
        }
    }
}

static const std::array<const wchar_t*, 15> ServicesToDisable = {
    L"xenagent",
    L"xenbus",
    L"xenbus_monitor",
    L"xencons",
    L"xencons_monitor",
    L"xendisk",
    L"xenfilt",
    L"xenhid",
    L"xeniface",
    L"XenInstall",
    L"xennet",
    L"XenSvc",
    L"xenvbd",
    L"xenvif",
    L"xenvkbd",
};

static void DisableServices(const CRegKey& controlSetKey) {
    for (auto serviceName : ServicesToDisable) {
        auto keyName = std::wstring(L"Services\\") + serviceName;
        CRegKey key;
        auto result = key.Open(controlSetKey, keyName.c_str(), KEY_ALL_ACCESS);
        if (result != ERROR_SUCCESS)
            continue;

        wprintf(L"Disabling service %s\n", serviceName);

        result = key.SetDWORDValue(L"Start", SERVICE_DISABLED);
        if (result != ERROR_SUCCESS) {
            wprintf(L"Couldn't disable %s: 0x%lx\n", serviceName, result);
            continue;
        }
    }
}

int wmain(int argc, wchar_t** argv) {
    if (argc != 2) {
        PrintUsage(argv[0]);
        return 1;
    }
    auto windir = argv[1];

    try {
        EnablePrivileges();

        auto configPath = std::filesystem::path(windir, std::filesystem::path::native_format);
        configPath /= L"System32\\config\\SYSTEM";
        wprintf(L"Opening hive \"%s\"\n", configPath.c_str());
        RegHive hive(HKEY_LOCAL_MACHINE, HiveMountName, configPath.c_str());

        DWORD controlSet = GetCurrentControlSet(hive);
        wprintf(L"Opening control set %lu\n", controlSet);
        CRegKey controlSetKey;
        OpenControlSet(hive, controlSetKey, controlSet);

        DeleteOverrides(controlSetKey);
        RemoveFilters(controlSetKey);
        DisableServices(controlSetKey);

        wprintf(L"Success!\n");
        wprintf(L"You must run XenClean from the VM to remove all remaining driver traces.\n");
    }
    catch (const std::exception& ex) {
        wprintf(L"Error: %S\n", ex.what());
        return 2;
    }
}
