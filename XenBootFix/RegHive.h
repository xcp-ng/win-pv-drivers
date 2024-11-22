#pragma once

#include <system_error>
#include <utility>
#include <Windows.h>

class RegHive {
public:
    RegHive(HKEY hkey, LPCWSTR subkey, LPCWSTR file) :_hkey(hkey), _subkey(subkey) {
        auto result = RegLoadKeyW(hkey, subkey, file);
        if (result != ERROR_SUCCESS)
            throw std::system_error(result, std::system_category(), "RegLoadKeyW");

    }
    RegHive(const RegHive&) = delete;
    RegHive& operator=(const RegHive&) = delete;
    RegHive(RegHive&& other) noexcept {
        swap(*this, other);
    }
    RegHive& operator=(RegHive&& other) noexcept {
        if (this != &other) {
            dispose();
            swap(*this, other);
        }
        return *this;
    };
    ~RegHive() {
        dispose();
    }

    constexpr HKEY HKey() const noexcept {
        return _hkey;
    }

    constexpr LPCWSTR SubKey() const noexcept {
        return _subkey;
    }

    int release() noexcept {
        dispose();
    }

    friend void swap(RegHive& self, RegHive& other) noexcept {
        using std::swap;
        swap(self._hkey, other._hkey);
        swap(self._subkey, other._subkey);
    }

private:
    HKEY _hkey = (HKEY)INVALID_HANDLE_VALUE;
    LPCWSTR _subkey = nullptr;

    void dispose() noexcept {
        if (_hkey != (HKEY)INVALID_HANDLE_VALUE) {
            auto result = RegUnLoadKeyW(_hkey, _subkey);
            if (result != ERROR_SUCCESS)
                wprintf(L"Cannot unload key: 0x%lx\n", result);
            _hkey = (HKEY)INVALID_HANDLE_VALUE;
            _subkey = nullptr;
        }
    }
};
