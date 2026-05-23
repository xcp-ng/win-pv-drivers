using Windows.Win32.Devices.DeviceAndDriverInstallation;

namespace XenPlus.XenIface;

sealed partial class XenIfaceSource {
    abstract class Request { }

    sealed class WorkerRequest : Request {
        public required CM_NOTIFY_ACTION Action { get; set; }
    }

    sealed class DeviceRequest : Request {
        public required CM_NOTIFY_ACTION Action { get; set; }
        public required XenIfaceDevice TargetDevice { get; set; }
    }

    sealed class ExitRequest : Request { }
}
