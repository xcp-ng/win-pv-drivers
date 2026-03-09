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

#ifndef UNICODE
#error "XenBootFix assumes Unicode!"
#endif

struct ThirdPartyStorageDriver {
    std::wstring DriverName;
    std::wstring ServiceName;
};

static constexpr auto HiveMountName = L"XenBootFix";

static bool dryrun = false;

static void PrintUsage(wchar_t *name) {
    wprintf(
        L"XenBootFix recovers an unbootable system caused by installation/uninstallation of older Xen PV drivers or OS "
        L"upgrades.\n");
    wprintf(L"Usage: %s [--force] [--backup <path>] [--dry-run] <windir>|--system-hive <hive>\n", name);
}

static std::vector<std::wstring> ParseMultiStrings(_In_reads_(count) const wchar_t *buf, size_t count) {
    std::vector<std::wstring> strings;
    if (!buf || !count)
        return strings;
    for (size_t i = 0; i < count; i++) {
        if (buf[i] == L'\0')
            break;
        size_t start = i;
        while (i < count && buf[i] != L'\0')
            i++;
        strings.emplace_back(buf + start, i - start);
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

static DWORD GetCurrentControlSet(const RegHive &hive) {
    auto keyName = std::wstring(hive.SubKey()) + L"\\Select";

    CRegKey selectKey;
    auto result = selectKey.Open(HKEY_LOCAL_MACHINE, keyName.c_str(), KEY_QUERY_VALUE);
    if (result != ERROR_SUCCESS)
        throw std::system_error(result, std::system_category(), "Couldn't open Select key");

    DWORD value;
    result = selectKey.QueryDWORDValue(L"Current", value);
    if (result != ERROR_SUCCESS)
        throw std::system_error(result, std::system_category(), "Couldn't query current hive");
    if (value == 0 || value > 999)
        throw std::runtime_error("Unexpected current control set value");

    return value;
}

static void OpenControlSet(const RegHive &hive, CRegKey &key, DWORD controlSet, REGSAM samDesired) {
    std::vector<wchar_t> csName(wcslen(hive.SubKey()) + wcslen(L"\\ControlSet000") + 1);
    auto csNameFormat = std::wstring(hive.SubKey()) + L"\\ControlSet%03lu";
    auto hr = StringCchPrintfW(csName.data(), csName.size(), csNameFormat.c_str(), controlSet);
    if (FAILED(hr))
        throw std::system_error(hr, std::system_category(), "StringCchPrintfW");

    auto result = key.Open(hive.HKey(), csName.data(), samDesired);
    if (result != ERROR_SUCCESS)
        throw std::system_error(hr, std::system_category(), "Couldn't open control set key");
}

static void Backup(CRegKey &key, const wchar_t *path) {
    auto result = RegSaveKeyExW(key, path, NULL, REG_LATEST_FORMAT);
    if (result != ERROR_SUCCESS)
        // Failure to back up is fatal
        throw std::system_error(result, std::system_category(), "Couldn't back up control set key");
}

static const std::array<const wchar_t *, 2> StorageClasses = {
    L"{4d36e96a-e325-11ce-bfc1-08002be10318}", // HDC
    L"{4d36e97b-e325-11ce-bfc1-08002be10318}", // SCSIAdapter
};

static bool StripStringNulls(std::wstring &s) {
    if (s.ends_with(wchar_t(0)))
        s.pop_back();
    return s.find(wchar_t(0)) == std::wstring::npos;
}

static LSTATUS
RegKeyQueryString(CRegKey &key, const wchar_t *valueName, std::wstring &value, ULONG lengthLimit = ULONG_MAX) {
    // Note that unlike RegQueryValueEx, CRegKey.QueryStringValue deals with chars and not bytes!

    value.clear();

    ULONG length = 0;
    auto result = key.QueryStringValue(valueName, NULL, &length);
    // limit guidLength since it's supposed to only contains a GUID
    if (result != ERROR_SUCCESS || length > lengthLimit) {
        return result;
    } else if (!length) {
        return ERROR_SUCCESS;
    }

    value.resize(length);
    result = key.QueryStringValue(valueName, value.data(), &length);
    if (result != ERROR_SUCCESS) {
        return result;
    }
    if (!StripStringNulls(value)) {
        wprintf(L"Refusing to process value with embedded nulls\n");
        return ERROR_INVALID_DATA;
    }

    return ERROR_SUCCESS;
}

static void
Scan3PStorageDriversOnNode(CRegKey &controlSetKey, CRegKey &nodeKey, std::vector<ThirdPartyStorageDriver> &output) {
    // Enumerate device subkeys of nodeKey = ControlSetXXX\Enum\PCI\VEN_XXX
    wchar_t keyName[255];
    for (DWORD i = 0;; i++) {
        DWORD nameLength = ARRAYSIZE(keyName);
        auto result = RegEnumKeyExW(nodeKey, i, keyName, &nameLength, NULL, NULL, NULL, NULL);
        switch (result) {
        case ERROR_SUCCESS: {
            keyName[nameLength] = 0;

            // functionKey =  Enum\PCI\VEN_XXX\<Device>

            CRegKey functionKey;
            result = functionKey.Open(nodeKey, keyName, KEY_READ);
            if (result != ERROR_SUCCESS) {
                wprintf(L"Couldn't open device node subkey: 0x%lx\n", result);
                return;
            }

            // Phase 1: see if functionKey ClassGUID value belongs to StorageClasses

            std::wstring classGuid;
            // limit guidLength since it's supposed to only contains a GUID
            result = RegKeyQueryString(functionKey, L"ClassGUID", classGuid, 128);
            if (result != ERROR_SUCCESS || classGuid.empty()) {
                wprintf(L"Couldn't query ClassGUID value: 0x%lx\n", result);
                return;
            }

            auto found = std::any_of(StorageClasses.begin(), StorageClasses.end(), [&](auto sc) {
                return CompareStringOrdinal(sc, -1, classGuid.c_str(), -1, TRUE) == CSTR_EQUAL;
            });
            if (found)
                wprintf(L"Device node \"%s\" class \"%s\"\n", keyName, classGuid.c_str());
            else
                continue;

            // Phase 2: get functionKey Driver value

            std::wstring driverInstance;
            // Driver value follows format Class\0000, want to limit size as well
            result = RegKeyQueryString(functionKey, L"Driver", driverInstance, 128);
            if (result != ERROR_SUCCESS || driverInstance.empty()) {
                wprintf(L"Couldn't query Driver value: 0x%lx\n", result);
                return;
            }

            // Phase 3: open driverKey = Control\Class\<Driver>

            auto driverKeyPath = L"Control\\Class\\" + driverInstance;
            CRegKey driverKey;
            result = driverKey.Open(controlSetKey, driverKeyPath.c_str(), KEY_READ);
            if (result != ERROR_SUCCESS) {
                wprintf(L"Couldn't open driverInstance key: 0x%lx\n", result);
                return;
            }

            // Phase 4: check driverKey InfPath

            std::wstring infPath;
            result = RegKeyQueryString(driverKey, L"InfPath", infPath, MAX_PATH);
            if (result != ERROR_SUCCESS || infPath.empty()) {
                wprintf(L"Couldn't query InfPath value: 0x%lx\n", result);
                return;
            }
            wprintf(L"Driver: \"%s\"\n", infPath.c_str());
            if (CompareStringOrdinal(infPath.c_str(), 3, L"oem", 3, TRUE) != CSTR_EQUAL)
                continue;

            // Phase 5: get functionKey Service value

            std::wstring serviceName;
            result = RegKeyQueryString(functionKey, L"Service", serviceName, MAX_PATH);
            if (result != ERROR_SUCCESS || serviceName.empty()) {
                wprintf(L"Couldn't query Service value: 0x%lx\n", result);
                return;
            }

            output.push_back(ThirdPartyStorageDriver{.DriverName = infPath, .ServiceName = serviceName});
            break;
        }
        case ERROR_NO_MORE_ITEMS:
            return;
        default:
            wprintf(L"Couldn't enumerate device node key: 0x%lx\n", result);
            return;
        }
    }
}

static void Scan3PStorageDrivers(CRegKey &controlSetKey, std::vector<ThirdPartyStorageDriver> &output) {
    CRegKey pciKey;
    // We only look at the PCI bus, which is a good-enough assumption on XCP VMs.
    // It also excludes the weird stuff (virtual disks etc.) and most importantly Xenbus.
    auto result = pciKey.Open(controlSetKey, L"Enum\\PCI", KEY_READ);
    if (result != ERROR_SUCCESS) {
        wprintf(L"Couldn't open Enum\\PCI key: 0x%lx\n", result);
        return;
    }

    wchar_t keyName[255];
    DWORD i = 0;
    for (DWORD i = 0;; i++) {
        DWORD nameLength = ARRAYSIZE(keyName);
        result = RegEnumKeyExW(pciKey, i, keyName, &nameLength, NULL, NULL, NULL, NULL);
        switch (result) {
        case ERROR_SUCCESS: {
            keyName[nameLength] = 0;
            wprintf(L"Enumerating PCI \"%s\"\n", keyName);

            CRegKey pciNodeKey;
            result = pciNodeKey.Open(pciKey, keyName, KEY_READ);
            if (result != ERROR_SUCCESS) {
                wprintf(L"Couldn't open Enum\\PCI subkey: 0x%lx\n", result);
                return;
            }
            Scan3PStorageDriversOnNode(controlSetKey, pciNodeKey, output);
            break;
        }
        case ERROR_NO_MORE_ITEMS:
            return;
        default:
            wprintf(L"Couldn't enumerate Enum\\PCI: 0x%lx\n", result);
            return;
        }
    }
}

static const std::array<const wchar_t *, 3> OverridesToDelete = {
    L"stornvme",
    L"storahci",
    L"pciide",
};

static void DeleteOverride(ATL::CRegKey &controlSetKey, const wchar_t *overrideName) {
    auto keyName = std::wstring(L"Services\\") + overrideName + L"\\StartOverride";
    wprintf(L"Deleting key \"%s\"\n", keyName.c_str());

    if (!dryrun) {
        auto result = controlSetKey.RecurseDeleteKey(keyName.c_str());
        if (result != ERROR_SUCCESS)
            wprintf(L"Couldn't delete key \"%s\": 0x%lx\n", keyName.c_str(), result);
    }
}

static void DeleteOverrides(CRegKey &controlSetKey, const std::vector<ThirdPartyStorageDriver> &found3PStorageDrivers) {
    for (auto overrideName : OverridesToDelete)
        DeleteOverride(controlSetKey, overrideName);
    for (const auto &driver : found3PStorageDrivers)
        DeleteOverride(controlSetKey, driver.ServiceName.c_str());
}

static void DeleteForceUnplug(CRegKey &controlSetKey) {
    wprintf(L"Deleting ForceUnplug\n");

    if (!dryrun) {
        auto result = controlSetKey.RecurseDeleteKey(L"Services\\XEN\\ForceUnplug");
        if (result != ERROR_SUCCESS)
            wprintf(L"Couldn't delete ForceUnplug: 0x%lx\n", result);
    }
}

static const std::array<const wchar_t *, 2> FiltersToRemove = {
    L"xenfilt",
    L"scsifilt",
};

static void RemoveFilter(CRegKey &key, const wchar_t *valueName) {
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
    for (const auto &filter : filters)
        wprintf(L"\"%s\", ", filter.c_str());
    wprintf(L"\n");

    std::vector<std::wstring> newFilters;
    for (const auto &filter : filters)
        if (std::none_of(FiltersToRemove.begin(), FiltersToRemove.end(), [&](auto &f) {
                return CompareStringOrdinal(filter.c_str(), -1, f, -1, TRUE) == CSTR_EQUAL;
            }))
            newFilters.emplace_back(filter);

    if (newFilters.empty()) {
        wprintf(L"New value \"%s\": <empty>\n", valueName);

        if (!dryrun) {
            result = key.DeleteValue(valueName);
            if (result != ERROR_SUCCESS) {
                wprintf(L"Couldn't delete value \"%s\": 0x%lx\n", valueName, result);
                return;
            }
        }
    } else {
        wprintf(L"New value \"%s\": ", valueName);
        for (const auto &filter : newFilters)
            wprintf(L"\"%s\", ", filter.c_str());
        wprintf(L"\n");

        bufsize = 1;
        for (const auto &filter : newFilters)
            bufsize += (ULONG)filter.size() + 1;
        buf.clear();
        buf.resize(bufsize, 0);

        wchar_t *ptr = buf.data();
        for (const auto &filter : newFilters) {
            ptr = std::copy(filter.begin(), filter.end(), ptr);
            *ptr++ = 0;
        }
        *ptr++ = 0;

        if (!dryrun) {
            result = key.SetMultiStringValue(valueName, buf.data());
            if (result != ERROR_SUCCESS) {
                wprintf(L"Couldn't set value \"%s\": 0x%lx\n", valueName, result);
                return;
            }
        }
    }
}

static const std::array<const wchar_t *, 2> FilteredClasses = {
    L"{4d36e96a-e325-11ce-bfc1-08002be10318}", // HDC
    L"{4d36e97d-e325-11ce-bfc1-08002be10318}", // System
};

static const std::array<const wchar_t *, 2> FilterValues = {
    L"LowerFilters",
    L"UpperFilters",
};

static void RemoveFilters(const CRegKey &controlSetKey) {
    for (auto clsid : FilteredClasses) {
        auto keyName = std::wstring(L"Control\\Class\\") + clsid;
        wprintf(L"Cleaning key \"%s\"\n", keyName.c_str());

        CRegKey key;
        auto result = key.Open(controlSetKey, keyName.c_str(), dryrun ? KEY_READ : KEY_ALL_ACCESS);
        if (result != ERROR_SUCCESS) {
            wprintf(L"Couldn't open key \"%s\": 0x%lx\n", keyName.c_str(), result);
            continue;
        }

        for (auto valueName : FilterValues) {
            RemoveFilter(key, valueName);
        }
    }
}

static const std::array<const wchar_t *, 15> ServicesToDisable = {
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

static void DisableServices(const CRegKey &controlSetKey) {
    for (auto serviceName : ServicesToDisable) {
        auto keyName = std::wstring(L"Services\\") + serviceName;
        CRegKey key;
        auto result = key.Open(controlSetKey, keyName.c_str(), dryrun ? KEY_READ : KEY_ALL_ACCESS);
        if (result != ERROR_SUCCESS)
            continue;

        wprintf(L"Disabling service \"%s\"\n", serviceName);

        if (!dryrun) {
            result = key.SetDWORDValue(L"Start", SERVICE_DISABLED);
            if (result != ERROR_SUCCESS) {
                wprintf(L"Couldn't disable \"%s\": 0x%lx\n", serviceName, result);
                continue;
            }
        }
    }
}

int wmain(int argc, wchar_t **argv) {
    wchar_t *windir = nullptr;
    std::wstring hivePath;
    bool force = false;
    wchar_t *backup = nullptr;

    for (int i = 1; i < argc; i++) {
        if (CompareStringOrdinal(L"--force", -1, argv[i], -1, TRUE) == CSTR_EQUAL) {
            force = true;
        } else if (CompareStringOrdinal(L"--backup", -1, argv[i], -1, TRUE) == CSTR_EQUAL) {
            if (i >= argc - 1)
                goto help;
            backup = argv[++i];
        } else if (CompareStringOrdinal(L"--dry-run", -1, argv[i], -1, TRUE) == CSTR_EQUAL) {
            dryrun = true;
        } else if (CompareStringOrdinal(L"--system-hive", -1, argv[i], -1, TRUE) == CSTR_EQUAL) {
            if (i >= argc - 1)
                goto help;
            hivePath = std::wstring(argv[++i]);
        } else if (!windir) {
            windir = argv[i];
        } else {
            goto help;
        }
    }
    if (!windir == hivePath.empty()) {
        goto help;
    } else if (windir) {
        auto configPath = std::filesystem::path(windir, std::filesystem::path::native_format);
        configPath /= L"System32\\config\\SYSTEM";
        hivePath = configPath.wstring();
    }

    try {
        {
            CRegKey winPeKey;
            if (winPeKey.Open(HKEY_LOCAL_MACHINE, L"SYSTEM\\CurrentControlSet\\Control\\MiniNT", KEY_READ) !=
                ERROR_SUCCESS) {
                wprintf(L"XenBootFix must run from within Windows PE/Windows RE!\n");
                if (force) {
                    wprintf(L"Continuing anyway. (--force)\n");
                } else {
                    wprintf(L"Specify --force to continue.\n");
                    throw std::runtime_error("XenBootFix must run from within Windows PE/Windows RE");
                }
            }
        }

        EnablePrivileges();

        wprintf(L"Opening hive \"%s\"\n", hivePath.c_str());
        RegHive hive(HKEY_LOCAL_MACHINE, HiveMountName, hivePath.c_str());

        DWORD controlSet = GetCurrentControlSet(hive);
        wprintf(L"Opening control set %03lu\n", controlSet);
        CRegKey controlSetKey;
        OpenControlSet(hive, controlSetKey, controlSet, dryrun ? KEY_READ : KEY_ALL_ACCESS);

        bool xenvbdPresent;
        {
            CRegKey xenvbdKey;
            auto result = xenvbdKey.Open(controlSetKey, L"Services\\xenvbd", KEY_READ);
            xenvbdPresent = result == ERROR_SUCCESS;
        }

        wprintf(L"Scanning for storage drivers\n");
        std::vector<ThirdPartyStorageDriver> found3PStorageDrivers;
        Scan3PStorageDrivers(controlSetKey, found3PStorageDrivers);
        if (!found3PStorageDrivers.empty()) {
            wprintf(L"Found third-party storage drivers!\n");
            for (const auto &driver : found3PStorageDrivers)
                wprintf(L"Driver: \"%s\", service: \"%s\"\n", driver.DriverName.c_str(), driver.ServiceName.c_str());
            // StartOverride issue only exists if xenvbd is installed
            if (xenvbdPresent) {
                wprintf(L"Xenvbd is currently enabled on your Windows installation.\n");
                wprintf(
                    L"In some cases, continuing with XenBootFix while third-party storage drivers are present may "
                    L"cause boot failures.\n");
                if (force) {
                    wprintf(L"Continuing anyway. (--force)\n");
                } else {
                    wprintf(L"If you want to continue anyway, specify --force.\n");
                    throw std::runtime_error("Found third-party storage drivers");
                }
            }
        }

        if (backup) {
            wprintf(L"Backing up SYSTEM hive to \"%s\"\n", backup);
            Backup(controlSetKey, backup);
        }

        DeleteOverrides(controlSetKey, found3PStorageDrivers);
        DeleteForceUnplug(controlSetKey);
        RemoveFilters(controlSetKey);
        DisableServices(controlSetKey);

        if (dryrun) {
            wprintf(L"Success! (dry-run)\n");
        } else {
            wprintf(L"Success!\n");
            wprintf(L"You must run XenClean from the VM to remove all remaining driver traces.\n");
        }

        return 0;
    } catch (const std::exception &ex) {
        wprintf(L"Error: %S\n", ex.what());
        return 2;
    }

help:
    PrintUsage(argv[0]);
    return 1;
}
