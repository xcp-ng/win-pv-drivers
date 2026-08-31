#include <Windows.h>

typedef void(__cdecl *CRT_INITFUNC)(void);

#define EI_ERROR_TRIPWIRE_HIT (MAKE_HRESULT(SEVERITY_ERROR, 0x999, 0x999) | (1L << 29))

static void __cdecl EarlyInitNAOT(void) {
    if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32)) {
        __fastfail(FAST_FAIL_FATAL_APP_EXIT);
    }
    if (GetEnvironmentVariableW(L"EarlyInitNAOT_C22459EB_2D32_4192_83B8_D054B8193F87", NULL, 0) != 0) {
        TerminateProcess(GetCurrentProcess(), (UINT)(EI_ERROR_TRIPWIRE_HIT));
    }
}

#pragma section(".CRT$XCT", read)
// prevent LTO from optimizing out our early initializer
#pragma comment(linker, "/include:_EarlyInitNAOT")
__declspec(allocate(".CRT$XCT")) CRT_INITFUNC _EarlyInitNAOT = EarlyInitNAOT;
