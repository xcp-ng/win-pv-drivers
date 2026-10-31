using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_NOTIFY_ACTION;
using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_NOTIFY_FILTER_TYPE;

namespace XenPlus;

/// <summary>
/// Keeps a duplicate device handle open to block query-remove and observes the resulting Windows notifications.
/// </summary>
sealed class XenIfaceQueryRemoveBlocker : IDisposable {
    readonly SafeFileHandle _duplicate;
    readonly GCHandle _gch;
    readonly CM_Unregister_NotificationSafeHandle _notification;
    readonly TaskCompletionSource _queryRemove = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource _queryRemoveFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool _removalObserved;
    bool _disposed;

    public Task QueryRemove {
        get {
            return _queryRemove.Task;
        }
    }

    public Task QueryRemoveFailed {
        get {
            return _queryRemoveFailed.Task;
        }
    }

    public bool RemovalObserved {
        get {
            return Volatile.Read(ref _removalObserved);
        }
    }

    public unsafe XenIfaceQueryRemoveBlocker(SafeFileHandle handle) {
        _duplicate = XenIfaceTestHandleDuplication.DuplicateFile(handle);
        try {
            _gch = GCHandle.Alloc(this);
            try {
                using var reference = _duplicate.Borrow();
                var filter = new CM_NOTIFY_FILTER {
                    cbSize = (uint)sizeof(CM_NOTIFY_FILTER),
                    FilterType = CM_NOTIFY_FILTER_TYPE_DEVICEHANDLE,
                    u = { DeviceHandle = { hTarget = (HANDLE)reference.Handle } }
                };
                // Reuse xenplus's CsWin32 declarations through IVT, keeping callback types identical to the source's.
                Cfgmgr32.CheckConfigret(PInvoke.CM_Register_Notification(
                    filter,
                    (void*)GCHandle.ToIntPtr(_gch),
                    &OnNotification,
                    out _notification));
            } catch {
                _gch.Free();
                throw;
            }
        } catch {
            _duplicate.Dispose();
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static unsafe uint OnNotification(
        HCMNOTIFICATION notification,
        void* context,
        CM_NOTIFY_ACTION action,
        CM_NOTIFY_EVENT_DATA* eventData,
        uint eventDataSize) {

        try {
            var self = Check.Unwrap<XenIfaceQueryRemoveBlocker>(GCHandle.FromIntPtr((nint)context).Target);
            if (action == CM_NOTIFY_ACTION_DEVICEQUERYREMOVE) {
                self._queryRemove.TrySetResult();
            } else if (action == CM_NOTIFY_ACTION_DEVICEQUERYREMOVEFAILED) {
                self._queryRemoveFailed.TrySetResult();
            } else if (action == CM_NOTIFY_ACTION_DEVICEREMOVEPENDING ||
                       action == CM_NOTIFY_ACTION_DEVICEREMOVECOMPLETE) {
                Volatile.Write(ref self._removalObserved, true);
            }
        } catch (Exception ex) {
            Environment.FailFast("Query-remove test observer unexpectedly failed", ex);
        }

        // Returning success does not permit removal while _duplicate remains open. The outstanding native handle
        // supplies the veto; this observer deliberately neither closes it nor returns ERROR_CANCELLED.
        return (uint)WIN32_ERROR.ERROR_SUCCESS;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }
        // Unregister outside the source lock and wait for callbacks before freeing their context.
        _notification.Dispose();
        _duplicate.Dispose();
        _gch.Free();
    }
}
