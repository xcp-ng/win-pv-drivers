using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;

namespace XenDriverUtils {
    public class XenDeviceInfo {
        public Guid ClassGuid { get; set; }
        public List<string> CompatibleIds { get; set; }

        public static readonly Dictionary<string, XenDeviceInfo> KnownDevices = new(StringComparer.OrdinalIgnoreCase) {
            {
                "Xenbus",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"PCI\\VEN_5853&DEV_{VersionInfo.VendorDeviceId}&SUBSYS_{VersionInfo.VendorDeviceId}5853&REV_01",
                        "PCI\\VEN_5853&DEV_0001",
                        "PCI\\VEN_5853&DEV_0002",
                        // Citrix
                        "PCI\\VEN_5853&DEV_C000&SUBSYS_C0005853&REV_01",
                    },
                }
            },
            {
                "Xencons",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_CONS&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_CONS&REV_09000000",
                    },
                }
            },
            {
                "Xenhid",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_HIDCLASS,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_HID&REV_09000000",
                        $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}0001&DEV_HID&REV_09000000",
                        $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}0002&DEV_HID&REV_09000000",
                    },
                }
            },
            {
                "Xeniface",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_IFACE&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_IFACE&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_IFACE&REV_09000000",
                        // Citrix
                        "XENBUS\\VEN_XSC000&DEV_IFACE&REV_09000000",
                        "XENBUS\\VEN_XS0001&DEV_IFACE&REV_09000000",
                        "XENBUS\\VEN_XS0002&DEV_IFACE&REV_09000000",
                    },
                }
            },
            {
                "Xennet",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_NET,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENVIF\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_NET&REV_09000000",
                        $"XENVIF\\VEN_{VersionInfo.VendorPrefix}0001&DEV_NET&REV_09000000",
                        $"XENVIF\\VEN_{VersionInfo.VendorPrefix}0002&DEV_NET&REV_09000000",
                        // Citrix
                        "XENVIF\\VEN_XSC000&DEV_NET&REV_09000000",
                        "XENVIF\\VEN_XS0001&DEV_NET&REV_09000000",
                        "XENVIF\\VEN_XS0002&DEV_NET&REV_09000000",
                    },
                }
            },
            {
                "Xenvbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SCSIADAPTER,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VBD&REV_09000000",
                        // Citrix
                        "XENBUS\\VEN_XSC000&DEV_VBD&REV_09000000",
                        "XENBUS\\VEN_XS0001&DEV_VBD&REV_09000000",
                        "XENBUS\\VEN_XS0002&DEV_VBD&REV_09000000",
                    },
                }
            },
            {
                "Xenvif",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VIF&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VIF&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VIF&REV_09000000",
                        // Citrix
                        "XENBUS\\VEN_XSC000&DEV_VIF&REV_09000000",
                        "XENBUS\\VEN_XS0001&DEV_VIF&REV_09000000",
                        "XENBUS\\VEN_XS0002&DEV_VIF&REV_09000000",
                    },
                }
            },
            {
                "Xenvkbd",
                new XenDeviceInfo() {
                    ClassGuid = PInvoke.GUID_DEVCLASS_SYSTEM,
                    CompatibleIds = new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId) ? null : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VKBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VKBD&REV_09000000",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VKBD&REV_09000000",
                    },
                }
            },
        };
    }
}
