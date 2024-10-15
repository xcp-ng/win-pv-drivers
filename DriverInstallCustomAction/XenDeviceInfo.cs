using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;

namespace XenInstCA {
    internal class XenDeviceInfo {
        public Guid ClassGuid { get; set; }
        public List<string> CompatibleIds { get; set; }
        public bool SafeInstall { get; set; }

        internal static readonly Dictionary<string, XenDeviceInfo> KnownDevices = new(StringComparer.OrdinalIgnoreCase) {
            {
                "Xenbus",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"PCI\\VEN_5853&DEV_{Version.VendorDeviceId}&SUBSYS_{Version.VendorDeviceId}5853&REV_01",
                        "PCI\\VEN_5853&DEV_0001",
                        "PCI\\VEN_5853&DEV_0002",
                    },
                    SafeInstall = false,
                }
            },
            {
                "Xencons",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"XENBUS\\VEN_{Version.VendorPrefix}{Version.VendorDeviceId}&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0001&DEV_CONS&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0002&DEV_CONS&REV_09000000",
                    },
                    SafeInstall = false,
                }
            },
            {
                "Xenhid",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_HIDCLASS,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"XENVKBD\\VEN_{Version.VendorPrefix}{Version.VendorDeviceId}&DEV_HID&REV_09000000",
                        $"XENVKBD\\VEN_{Version.VendorPrefix}0001&DEV_HID&REV_09000000",
                        $"XENVKBD\\VEN_{Version.VendorPrefix}0002&DEV_HID&REV_09000000",
                    },
                    SafeInstall = false,
                }
            },
            {
                "Xeniface",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"XENBUS\\VEN_{Version.VendorPrefix}{Version.VendorDeviceId}&DEV_IFACE&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0001&DEV_IFACE&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0002&DEV_IFACE&REV_09000000",
                    },
                    SafeInstall = false,
                }
            },
            {
                "Xennet",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_NET,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"XENVIF\\VEN_{Version.VendorPrefix}{Version.VendorDeviceId}&DEV_NET&REV_09000000",
                        $"XENVIF\\VEN_{Version.VendorPrefix}0001&DEV_NET&REV_09000000",
                        $"XENVIF\\VEN_{Version.VendorPrefix}0002&DEV_NET&REV_09000000",
                    },
                    SafeInstall = true,
                }
            },
            {
                "Xenvbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SCSIADAPTER,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"XENBUS\\VEN_{Version.VendorPrefix}{Version.VendorDeviceId}&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0001&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{ Version.VendorPrefix}0002&DEV_VBD&REV_09000000",
                    },
                    SafeInstall = true,
                }
            },
            {
                "Xenvif",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"XENBUS\\VEN_{Version.VendorPrefix}{Version.VendorDeviceId}&DEV_VIF&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0001&DEV_VIF&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0002&DEV_VIF&REV_09000000",
                    },
                    SafeInstall = false,
                }
            },
            {
                "Xenvkbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(Version.VendorDeviceId) ? null : $"XENBUS\\VEN_{Version.VendorPrefix}{Version.VendorDeviceId}&DEV_VKBD&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0001&DEV_VKBD&REV_09000000",
                        $"XENBUS\\VEN_{Version.VendorPrefix}0002&DEV_VKBD&REV_09000000",
                    },
                    SafeInstall = false,
                }
            },
        };
    }
}
