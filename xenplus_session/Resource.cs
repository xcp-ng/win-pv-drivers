using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;

namespace XenPlus;

static class Resources {
    public const ushort Icon = 101;

    public const ushort TrayMenu = 200;
    public const ushort TrayMenu_Hide = 201;
    public const ushort TrayMenu_About = 208;
    public const ushort TrayMenu_Exit = 209;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe PCWSTR MAKEINTRESOURCE(ushort id) {
        return (PCWSTR)(char*)(nint)id;
    }

    public static DestroyIconSafeHandle LoadIcon(ushort resourceId, bool large) {
        using var hinst = PInvoke.GetModuleHandle(null);
        using var hinstScope = hinst.Borrow();
        HICON phicon;
        unsafe {
            PInvoke.LoadIconMetric(
                (HINSTANCE)hinstScope.DangerousHandle,
                MAKEINTRESOURCE(resourceId),
                large ? _LI_METRIC.LIM_LARGE : _LI_METRIC.LIM_SMALL,
                &phicon).ThrowOnFailure();
        }
        return new(phicon, true);
    }

    public static DestroyMenuSafeHandle LoadMenu(ushort resourceId) {
        using var hinst = PInvoke.GetModuleHandle(null);
        using var hinstScope = hinst.Borrow();
        unsafe {
            var hmenu = PInvoke.LoadMenu(
                (HINSTANCE)hinstScope.DangerousHandle,
                MAKEINTRESOURCE(resourceId));
            if (hmenu == HMENU.Null) {
                throw new Win32Exception(nameof(PInvoke.LoadMenu));
            }
            return new(hmenu, true);
        }
    }
}
