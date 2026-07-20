using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_NOTIFY_ACTION;
using static Windows.Win32.Devices.DeviceAndDriverInstallation.CM_NOTIFY_FILTER_TYPE;

namespace XenPlus.XenIface;

sealed partial class XenIfaceSource : IDisposable {
    static readonly Guid GUID_INTERFACE_XENIFACE = new(
        0xb2cfb085,
        0xaa5e,
        0x47e1,
        0x8b, 0xf7,
        0x97, 0x93, 0xf3, 0x15, 0x45, 0x65);

    readonly ILogger _logger;

    CancellationTokenRegistration? _unregister;

    /// <summary>
    /// unlocked but readonly during lifetime
    /// </summary>
    readonly AutoResetEvent _suspend;
    /// <summary>
    /// unlocked but readonly during lifetime
    /// </summary>
    readonly RegisteredWaitHandle _suspendWait;
    bool _suspendWaitRegistered = false;

    readonly Thread _worker;

    readonly object _lock = new();
    /// <summary>
    /// locked
    /// </summary>
    readonly Queue<Request> _requests = new();
    /// <summary>
    /// locked
    /// </summary>
    XenIfaceDevice? _active = null;
    /// <summary>
    /// locked
    /// </summary>
    readonly HashSet<XenIfaceWatch> _watches = [];

    bool _disposed = false;

    /// <summary>
    /// For internal XenIface consumption only.
    /// </summary>
    internal object SyncRoot => _lock;
    /// <summary>
    /// For internal XenIface consumption only.
    /// </summary>
    internal XenIfaceDevice? Active => _active;

    void SuspendCallback() {
        lock (_lock) {
            if (_active == null) {
                return;
            }
            foreach (var watch in _watches) {
                _logger.LogTrace("Rearming watch: '{}'", watch.Path);
                watch.Rearm(_active);
            }
        }
        Resumed?.Invoke(this, new XenIfaceResumedEventArgs());
    }

    public XenIfaceSource(ILogger<XenIfaceSource> logger, CancellationToken? ct = null) {
        _logger = logger;
        try {
            _unregister = ct?.Register(() => {
                lock (_lock) {
                    _requests.Enqueue(new ExitRequest());
                    Monitor.Pulse(_lock);
                }
            });

            _suspend = new(false);
            _suspendWait = ThreadPool.RegisterWaitForSingleObject(_suspend, (state, _) => {
                Check.Unwrap<XenIfaceSource>(state).SuspendCallback();
            }, this, Timeout.Infinite, false);
            _suspendWaitRegistered = true;

            _worker = new Thread(Worker) {
                IsBackground = true,
                Name = nameof(XenIfaceSource),
            };
            _worker.Start();

            // fake an arrival for initial scan
            lock (_lock) {
                _requests.Enqueue(new WorkerRequest() { Action = CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL });
                Monitor.Pulse(_lock);
            }
        } catch {
            Dispose();
            throw;
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }

        if (_unregister.HasValue) {
            _unregister?.Dispose();
            _unregister = null;
        }

        if (_worker.IsAlive) {
            lock (_lock) {
                _requests.Enqueue(new ExitRequest());
                Monitor.Pulse(_lock);
            }
            _worker.Join();
        }

        if (_suspendWaitRegistered) {
            _suspendWait.Unregister(null);
            _suspendWaitRegistered = false;
        }
        _suspend.Dispose();
    }

    public XenIfaceHandle Lock() {
        return new XenIfaceHandle(this);
    }

    public delegate void XenIfaceResumedEventHandler(object? sender, XenIfaceResumedEventArgs args);

    /// <summary>
    /// Event handler runs in an arbitrary thread. Event triggers may overlap.
    /// </summary>
    public event XenIfaceResumedEventHandler? Resumed;

    /// <summary>
    /// locked
    /// </summary>
    void RefreshDevices(out XenIfaceDevice? lastActive) {
        lastActive = null;
        if (_active != null) {
            if (!_active.Handle.IsInvalid) {
                return;
            } else {
                lastActive = _active;
                _active = null;
            }
        }

        foreach (var device in Cfgmgr32.GetDeviceInterfaces(GUID_INTERFACE_XENIFACE)) {
            try {
                _logger.LogTrace("Trying {device}", device);
                _active = new XenIfaceDevice(this, device);
                _logger.LogDebug("Opened {device}", device);
                break;
            } catch (Exception ex) {
                _logger.LogTrace(ex, "Failed to open device {device}", device);
            }
        }

        if (_active == null) {
            throw new XenIfaceNotFoundException("No active device");
        }

        _active.SuspendRegister(_suspend.SafeWaitHandle);
        _suspend.Set();
    }

    internal void OnDeviceCallback(CM_NOTIFY_ACTION action, XenIfaceDevice target) {
        lock (_lock) {
            _requests.Enqueue(new DeviceRequest() {
                Action = action,
                TargetDevice = target
            });
        }
    }

    internal XenIfaceWatch WatchAddLocked(XenIfaceDevice? device, string path) {
        AutoResetEvent? evt = null;
        XenIfaceWatchImpl? result = null;
        try {
            evt = new AutoResetEvent(false);
            result = new XenIfaceWatchImpl(path, this, evt, device);
            evt = null;
            _watches.Add(result);
            return result;
        } catch {
            result?.Dispose();
            evt?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Add a watch even without a valid handle. The watch will be rearmed when a device appears.
    /// </summary>
    public XenIfaceWatch WatchAdd(string path) {
        lock (_lock) {
            return WatchAddLocked(_active, path);
        }
    }

    internal void WatchUnregister(XenIfaceWatch watch) {
        lock (_lock) {
            _watches.Remove(watch);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static unsafe uint WorkerCmCallback(
        HCMNOTIFICATION notifyHandle,
        void* context,
        CM_NOTIFY_ACTION action,
        CM_NOTIFY_EVENT_DATA* eventData,
        uint eventDataSize) {
        try {
            var gch = GCHandle.FromIntPtr((nint)context);
            var self = Check.Unwrap<XenIfaceSource>(gch.Target);
            lock (self._lock) {
                self._requests.Enqueue(new WorkerRequest() { Action = action });
                Monitor.Pulse(self._lock);
            }
        } catch (Exception ex) {
            Environment.FailFast("Worker CM callback unexpectedly failed", ex);
        }
        return (uint)WIN32_ERROR.ERROR_SUCCESS;
    }

    unsafe void Worker(object? _param) {
        GCHandle gch = GCHandle.Alloc(this);
        try {
            var filter = new CM_NOTIFY_FILTER {
                cbSize = (uint)sizeof(CM_NOTIFY_FILTER),
                Flags = 0,
                FilterType = CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE,
                Reserved = 0,
                u = { DeviceInterface = { ClassGuid = GUID_INTERFACE_XENIFACE } }
            };
            Cfgmgr32.CheckConfigret(PInvoke.CM_Register_Notification(
                filter,
                (void*)GCHandle.ToIntPtr(gch),
                &WorkerCmCallback,
                out var cmWorker));

            using (cmWorker) {
                using var tombstones = new DisposeStack();

                while (true) {
                    // due to the worker thread needing a separate unlocked section, we can't use BlockingCollection or
                    // similar blocking queues, and have to base the worker thread off of a monitor instead
                    lock (_lock) {
                        // pulses can get lost when the worker is in the unlocked section below, so we need to watch the
                        // predicate
                        while (_requests.Count == 0) {
                            Monitor.Wait(_lock);
                        }

                        while (_requests.TryDequeue(out var request)) {
                            if (request is WorkerRequest workerRequest &&
                                workerRequest.Action == CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL) {
                                try {
                                    RefreshDevices(out var lastActive);
                                    if (lastActive != null) {
                                        _logger.LogDebug("killing last active {}", lastActive.DevicePath);
                                        tombstones.Push(lastActive);
                                    }
                                } catch (XenIfaceNotFoundException ex) {
                                    _logger.LogInformation(ex, "Did not find Xen PV interface. This is a transient error.");
                                }

                            } else if (request is DeviceRequest listenerRequest &&
                                  (listenerRequest.Action == CM_NOTIFY_ACTION_DEVICEREMOVEPENDING ||
                                  listenerRequest.Action == CM_NOTIFY_ACTION_DEVICEREMOVECOMPLETE)) {
                                if (ReferenceEquals(listenerRequest.TargetDevice, _active)) {
                                    _active = null;
                                }
                                _logger.LogDebug(
                                    "{} on {}",
                                    listenerRequest.Action,
                                    listenerRequest.TargetDevice.DevicePath);
                                tombstones.Push(listenerRequest.TargetDevice);

                            } else if (request is ExitRequest) {
                                _logger.LogDebug("exiting worker");
                                return;
                            }
                        }
                    }

                    // closing old listeners must be done outside of the lock, since CM_Unregister_Notification will
                    // wait for callbacks to finish
                    tombstones.Dispose();
                }
            }
        } finally {
            gch.Free();
        }
    }
}
