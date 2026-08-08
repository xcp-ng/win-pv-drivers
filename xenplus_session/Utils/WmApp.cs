using Windows.Win32;

namespace XenPlus;

enum WmApp : uint {
    WM_APP = PInvoke.WM_APP,
    TrayMenuMessage,
}
