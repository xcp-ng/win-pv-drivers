using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Win32;

namespace XenDriverUtils {
    public class XenDeviceInfo {
        public Guid? ClassGuid { get; }
        public IReadOnlyList<string> HardwareIds { get; }
        public IReadOnlyList<string> IncompatibleIdRegexes { get; } = new List<string>();

        public XenDeviceInfo(
            Guid? ClassGuid,
            IEnumerable<string> HardwareIds,
            IEnumerable<string> IncompatibleIdRegexes = null) {
            this.ClassGuid = ClassGuid;
            this.HardwareIds = new List<string>(HardwareIds);
            if (IncompatibleIdRegexes is not null) {
                this.IncompatibleIdRegexes = new List<string>(IncompatibleIdRegexes);
            }
        }

        public bool MatchesId(string deviceId, bool checkKnown, bool checkIncompatible) {
            if (string.IsNullOrEmpty(deviceId))
                return false;
            var matchesHardwareId = HardwareIds.Any(substr => substr != null && deviceId.StartsWith(substr, StringComparison.OrdinalIgnoreCase));
            if (checkKnown && matchesHardwareId)
                return true;
            if (checkIncompatible
                && !matchesHardwareId
                && IncompatibleIdRegexes.Any(re => re != null && Regex.IsMatch(deviceId, re, RegexOptions.IgnoreCase)))
                return true;
            return false;
        }

        public static readonly IReadOnlyDictionary<string, XenDeviceInfo> KnownDevices
            = new Dictionary<string, XenDeviceInfo>(StringComparer.OrdinalIgnoreCase) {
            {
                "Xenbus",
                new XenDeviceInfo(
                    // Note: ClassGuid is set to null here to remove non-present Xenbus devices, which may belong to an
                    // unknown class.
                    ClassGuid: null,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"PCI\\VEN_5853&DEV_{VersionInfo.VendorDeviceId}&SUBSYS_{VersionInfo.VendorDeviceId}5853&REV_01",
                        // these two entries cover all Xenbus devices
                        "PCI\\VEN_5853&DEV_0001",
                        "PCI\\VEN_5853&DEV_0002",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^PCI\\\\VEN_5853&DEV_(?<id>[0-9A-F]{4})&SUBSYS_\\k<id>5853&REV_01"
                    }
                )
            },
            {
                "Xencons",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_CONS&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_CONS&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_CONS&REV_09",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^XENBUS\\\\VEN_[A-Z0-9]{2}[0-9A-F]{4}&DEV_CONS",
                    }
                )
            },
            {
                "Xenhid",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_HIDCLASS,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_HID&REV_09",
                        $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}0001&DEV_HID&REV_09",
                        $"XENVKBD\\VEN_{VersionInfo.VendorPrefix}0002&DEV_HID&REV_09",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^XENVKBD\\\\VEN_[A-Z0-9]{2}[0-9A-F]{4}&DEV_HID",
                    }
                )
            },
            {
                "Xeniface",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_IFACE&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_IFACE&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_IFACE&REV_09",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^XENBUS\\\\VEN_[A-Z0-9]{2}[0-9A-F]{4}&DEV_IFACE",
                    }
                )
            },
            {
                "Xennet",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_NET,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"XENVIF\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_NET&REV_09",
                        $"XENVIF\\VEN_{VersionInfo.VendorPrefix}0001&DEV_NET&REV_09",
                        $"XENVIF\\VEN_{VersionInfo.VendorPrefix}0002&DEV_NET&REV_09",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^XENVIF\\\\VEN_[A-Z0-9]{2}[0-9A-F]{4}&DEV_NET",
                    }
                )
            },
            {
                "Xenvbd",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_SCSIADAPTER,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VBD&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VBD&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VBD&REV_09",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^XENBUS\\\\VEN_[A-Z0-9]{2}[0-9A-F]{4}&DEV_VBD",
                    }
                )
            },
            {
                "Xenvif",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VIF&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VIF&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VIF&REV_09",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^XENBUS\\\\VEN_[A-Z0-9]{2}[0-9A-F]{4}&DEV_VIF",
                    }
                )
            },
            {
                "Xenvkbd",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_SYSTEM,
                    HardwareIds: new List<string>() {
                        string.IsNullOrEmpty(VersionInfo.VendorDeviceId)
                            ? null
                            : $"XENBUS\\VEN_{VersionInfo.VendorPrefix}{VersionInfo.VendorDeviceId}&DEV_VKBD&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0001&DEV_VKBD&REV_09",
                        $"XENBUS\\VEN_{VersionInfo.VendorPrefix}0002&DEV_VKBD&REV_09",
                    },
                    IncompatibleIdRegexes: new List<string>() {
                        "^XENBUS\\\\VEN_[A-Z0-9]{2}[0-9A-F]{4}&DEV_VKBD",
                    }
                )
            },
            // non-Xen
            {
                "Xstdvga",
                new XenDeviceInfo(
                    ClassGuid: PInvoke.GUID_DEVCLASS_DISPLAY,
                    HardwareIds: new List<string>() {
                        $"PCI\\VEN_1234&DEV_1111&CC_0300",
                        $"PCI\\VEN_1234&DEV_1111&CC_0380",
                    }
                )
            },
            // pseudo-devices
            {
                "Xendevice",
                new XenDeviceInfo(
                    ClassGuid: null,
                    HardwareIds: new List<string>() {
                        "XENDEVICE",
                    }
                )
            },
            {
                "Xenclass",
                new XenDeviceInfo(
                    ClassGuid: null,
                    HardwareIds: new List<string>() {
                        "XENCLASS",
                    }
                )
            },
        };
    }
}
