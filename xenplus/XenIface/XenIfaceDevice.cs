using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_NOTIFY_ACTION;
using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_NOTIFY_FILTER_TYPE;

namespace XenPlus.XenIface;

sealed partial class XenIfaceDevice : IDisposable {
    readonly GCHandle _gch;
    readonly XenIfaceSource _parent;
    public string DevicePath { get; }
    public SafeFileHandle Handle { get; }
    bool _pinned = false;
    readonly CM_Unregister_NotificationSafeHandle _cmDevice;
    bool _disposed = false;

    internal unsafe XenIfaceDevice(XenIfaceSource parent, string devicePath) {
        try {
            _gch = GCHandle.Alloc(this);

            _parent = parent;
            DevicePath = devicePath;
            Handle = File.OpenHandle(
                devicePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);

            var filter = new CM_NOTIFY_FILTER {
                cbSize = (uint)sizeof(CM_NOTIFY_FILTER),
                Flags = 0,
                FilterType = CM_NOTIFY_FILTER_TYPE_DEVICEHANDLE,
                Reserved = 0,
                u = { DeviceHandle = { hTarget = (HANDLE)Handle.DangerousGetHandle() } }
            };
            Utils.CheckConfigret(PInvoke.CM_Register_Notification(
                filter,
                (void*)GCHandle.ToIntPtr(_gch),
                &DeviceCmCallback,
                out _cmDevice));
        } catch {
            Dispose();
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static unsafe uint DeviceCmCallback(
        HCMNOTIFICATION notifyHandle,
        void* context,
        CM_NOTIFY_ACTION action,
        CM_NOTIFY_EVENT_DATA* eventData,
        uint eventDataSize) {
        try {
            var gch = GCHandle.FromIntPtr((nint)context);
            var self = Utils.Unwrap<XenIfaceDevice>(gch.Target);

            if (action == CM_NOTIFY_ACTION_DEVICEQUERYREMOVE ||
                action == CM_NOTIFY_ACTION_DEVICEQUERYREMOVEFAILED) {
                // must close immediately to avoid failing DEVICEQUERYREMOVE
                self.Handle.Close();
            }

            self._parent.OnDeviceCallback(action, self);
        } catch (Exception ex) {
            Environment.FailFast("Device CM callback unexpectedly failed", ex);
        }
        return (uint)WIN32_ERROR.ERROR_SUCCESS;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }
        // note the backwards destruction order
        Handle.Dispose();
        _cmDevice.Dispose();
        _gch.Free();
    }
}
