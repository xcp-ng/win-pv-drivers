using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;

namespace XenDriverUtils {
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DevInstallParams {
        public uint cbSize;
        public SETUP_DI_DEVICE_INSTALL_FLAGS Flags;
        public uint FlagsEx;
        public IntPtr hwndParent;
        public IntPtr InstallMsgHandler;
        public IntPtr InstallMsgHandlerContext;
        public IntPtr FileQueue;
        public UIntPtr ClassInstallReserved;
        public uint Reserved;
        public fixed ushort DriverPath[(int)PInvoke.MAX_PATH];

        public static uint Size => (uint)sizeof(DevInstallParams);

        public void SetDriverPath(string value) {
            fixed (ushort* p = DriverPath) {
                SetupDiInterop.WriteString(p, (int)PInvoke.MAX_PATH, value, nameof(DriverPath));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DrvInfoData {
        public uint cbSize;
        public SETUP_DI_DRIVER_TYPE DriverType;
        public UIntPtr Reserved;
        public fixed ushort Description[(int)PInvoke.LINE_LEN];
        public fixed ushort MfgName[(int)PInvoke.LINE_LEN];
        public fixed ushort ProviderName[(int)PInvoke.LINE_LEN];
        public uint DriverDateLow;
        public uint DriverDateHigh;
        public uint DriverVersionLow;
        public uint DriverVersionHigh;

        public static uint Size => (uint)sizeof(DrvInfoData);
    }

    internal static unsafe partial class SetupDiInterop {
#if NET7_0_OR_GREATER
        [LibraryImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInstallParamsW", SetLastError = true)]
        private static partial int SetupDiGetDeviceInstallParamsW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            void* deviceInstallParams);

        [LibraryImport("setupapi.dll", EntryPoint = "SetupDiSetDeviceInstallParamsW", SetLastError = true)]
        private static partial int SetupDiSetDeviceInstallParamsW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            void* deviceInstallParams);

        [LibraryImport("setupapi.dll", EntryPoint = "SetupDiEnumDriverInfoW", SetLastError = true)]
        private static partial int SetupDiEnumDriverInfoW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            uint driverType,
            uint memberIndex,
            void* driverInfoData);

        [LibraryImport("setupapi.dll", EntryPoint = "SetupDiSetSelectedDriverW", SetLastError = true)]
        private static partial int SetupDiSetSelectedDriverW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            void* driverInfoData);
#else
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInstallParamsW", ExactSpelling = true, SetLastError = true)]
        private static extern int SetupDiGetDeviceInstallParamsW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            void* deviceInstallParams);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiSetDeviceInstallParamsW", ExactSpelling = true, SetLastError = true)]
        private static extern int SetupDiSetDeviceInstallParamsW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            void* deviceInstallParams);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiEnumDriverInfoW", ExactSpelling = true, SetLastError = true)]
        private static extern int SetupDiEnumDriverInfoW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            uint driverType,
            uint memberIndex,
            void* driverInfoData);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiSetSelectedDriverW", ExactSpelling = true, SetLastError = true)]
        private static extern int SetupDiSetSelectedDriverW(
            IntPtr deviceInfoSet,
            void* deviceInfoData,
            void* driverInfoData);
#endif

        public static bool GetDeviceInstallParams(
            SafeHandle devInfo,
            SP_DEVINFO_DATA devInfoData,
            ref DevInstallParams installParams) {
            var addRef = false;
            try {
                devInfo.DangerousAddRef(ref addRef);
                fixed (DevInstallParams* p = &installParams) {
                    return SetupDiGetDeviceInstallParamsW(devInfo.DangerousGetHandle(), &devInfoData, p) != 0;
                }
            } finally {
                if (addRef) {
                    devInfo.DangerousRelease();
                }
            }
        }

        public static bool SetDeviceInstallParams(
            SafeHandle devInfo,
            SP_DEVINFO_DATA devInfoData,
            ref DevInstallParams installParams) {
            var addRef = false;
            try {
                devInfo.DangerousAddRef(ref addRef);
                fixed (DevInstallParams* p = &installParams) {
                    return SetupDiSetDeviceInstallParamsW(devInfo.DangerousGetHandle(), &devInfoData, p) != 0;
                }
            } finally {
                if (addRef) {
                    devInfo.DangerousRelease();
                }
            }
        }

        public static bool EnumDriverInfo(
            SafeHandle devInfo,
            SP_DEVINFO_DATA devInfoData,
            SETUP_DI_DRIVER_TYPE driverType,
            uint index,
            ref DrvInfoData drvInfoData) {
            var addRef = false;
            try {
                devInfo.DangerousAddRef(ref addRef);
                fixed (DrvInfoData* p = &drvInfoData) {
                    return SetupDiEnumDriverInfoW(
                        devInfo.DangerousGetHandle(),
                        &devInfoData,
                        (uint)driverType,
                        index,
                        p) != 0;
                }
            } finally {
                if (addRef) {
                    devInfo.DangerousRelease();
                }
            }
        }

        public static bool SetSelectedDriver(
            SafeHandle devInfo,
            SP_DEVINFO_DATA devInfoData,
            ref DrvInfoData drvInfoData) {
            var addRef = false;
            try {
                devInfo.DangerousAddRef(ref addRef);
                fixed (DrvInfoData* p = &drvInfoData) {
                    return SetupDiSetSelectedDriverW(devInfo.DangerousGetHandle(), &devInfoData, p) != 0;
                }
            } finally {
                if (addRef) {
                    devInfo.DangerousRelease();
                }
            }
        }

        internal static void WriteString(ushort* p, int maxChars, string value, string fieldName) {
            if (value.Length >= maxChars) {
                throw new ArgumentException($"{fieldName} too long ({value.Length} chars): {value}");
            }
            for (var i = 0; i < maxChars; i++) {
                p[i] = i < value.Length ? value[i] : (ushort)0;
            }
        }
    }
}
