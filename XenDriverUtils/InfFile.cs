using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

namespace XenDriverUtils {
    public class InfFile : SafeHandleMinusOneIsInvalid {
        public InfFile(IntPtr existingHandle, bool ownsHandle) : base(ownsHandle) {
            handle = existingHandle;
        }

        public static InfFile Open(string FileName, string InfClass, INF_STYLE InfStyle, out uint ErrorLine) {
            IntPtr infHandle = HANDLE.INVALID_HANDLE_VALUE;
            unsafe {
                fixed (uint* pErrorLine = &ErrorLine) {
                    infHandle = (IntPtr)PInvoke.SetupOpenInfFile(FileName, InfClass, InfStyle, pErrorLine);
                }
            }
            if (infHandle == HANDLE.INVALID_HANDLE_VALUE) {
                var err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, $"SetupOpenInfFile {err}");
            }
            return new InfFile(infHandle, true);
        }

        private INFCONTEXT FindFirstLine(string section, string key) {
            if (string.IsNullOrEmpty(section)) {
                throw new ArgumentNullException("Need valid section name");
            }
            unsafe {
                if (!PInvoke.SetupFindFirstLine(handle.ToPointer(), section, key, out var context)) {
                    var err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"Inf cannot find line {section}/{key} {err}");
                }
                return context;
            }
        }

        private INFCONTEXT FindNextLine(INFCONTEXT contextIn) {
            unsafe {
                if (!PInvoke.SetupFindNextLine(contextIn, out var contextOut)) {
                    var err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"Inf cannot find next line {err}");
                }
                return contextOut;
            }
        }

        private string GetLineText(ref INFCONTEXT context) {
            unsafe {
                fixed (INFCONTEXT* pContext = &context) {
                    uint requiredChars;
                    if (!PInvoke.SetupGetLineText(pContext, null, null, null, null, 0, &requiredChars)) {
                        var err = Marshal.GetLastWin32Error();
                        throw new Win32Exception(err, $"Inf cannot get line size {err}");
                    }
                    var mem = Marshal.AllocHGlobal((int)requiredChars * sizeof(char));
                    try {
                        if (!PInvoke.SetupGetLineText(pContext, null, null, null, new PWSTR((char*)mem.ToPointer()), requiredChars, null)) {
                            var err = Marshal.GetLastWin32Error();
                            throw new Win32Exception(err, $"Inf cannot get line {err}");
                        }
                        return Marshal.PtrToStringUni(mem);
                    } finally {
                        Marshal.FreeHGlobal(mem);
                    }
                }
            }
        }

        private string GetStringField(ref INFCONTEXT context, uint fieldIndex) {
            unsafe {
                fixed (INFCONTEXT* pContext = &context) {
                    uint requiredSize;
                    if (!PInvoke.SetupGetStringField(pContext, fieldIndex, null, 0, &requiredSize)) {
                        var err = Marshal.GetLastWin32Error();
                        throw new Win32Exception(err, $"Inf cannot get field size {err}");
                    }
                    var mem = Marshal.AllocHGlobal((int)requiredSize * sizeof(char));
                    try {
                        if (!PInvoke.SetupGetStringField(pContext, fieldIndex, new PWSTR((char*)mem.ToPointer()), requiredSize, null)) {
                            var err = Marshal.GetLastWin32Error();
                            throw new Win32Exception(err, $"Inf cannot get field {err}");
                        }
                        return Marshal.PtrToStringUni(mem);
                    } finally {
                        Marshal.FreeHGlobal(mem);
                    }
                }
            }
        }

        public string GetStringField(string section, string key, uint fieldIndex) {
            try {
                var line = FindFirstLine(section, key);
                return GetStringField(ref line, fieldIndex);
            } catch (Exception ex) {
                throw new KeyNotFoundException($"{section}/{key}", ex);
            }
        }

        protected override bool ReleaseHandle() {
            if (!IsInvalid) {
                unsafe {
                    PInvoke.SetupCloseInfFile(handle.ToPointer());
                }
            }
            return true;
        }
    }
}
