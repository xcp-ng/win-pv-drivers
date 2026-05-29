namespace XenPlus.XenIface;

sealed partial class XenIfaceSource {
    /// <summary>
    /// Lock handle of a <c>XenIface</c> object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both <c>class</c> and <c>ref struct</c> are inappropriate for this sort of handle object, the former because the
    /// handle can escape its scope/thread (and therefore violate the thread requirements of <c>Monitor</c>), and the
    /// latter because the handle can be copied. Neither are that optimal... At least <c>ref struct</c> doesn't require
    /// an allocation.
    /// </para>
    /// <para>
    /// Needless to say, you should never copy this struct.
    /// </para>
    /// </remarks>
    public ref struct XenIfaceHandle {
        XenIfaceSource? _parent;

        public string? DevicePath { get; }
        readonly XenIfaceDevice Active {
            get {
                ObjectDisposedException.ThrowIf(_parent == null, typeof(XenIfaceHandle));
                return _parent._active ?? throw new XenIfaceNotFoundException("no active device");
            }
        }

        internal XenIfaceHandle(XenIfaceSource parent) {
            try {
                Monitor.Enter(parent._lock);
                _parent = parent;
                DevicePath = _parent._active?.DevicePath;
            } catch {
                Dispose();
                throw;
            }
        }

        public void Dispose() {
            if (_parent != null) {
                Monitor.Exit(_parent._lock);
                _parent = null;
            }
        }

        public readonly string StoreRead(string path, bool strict = false) {
            return Active.StoreRead(path, strict);
        }

        public readonly void StoreWrite(string path, string? value, bool strict = false) {
            Active.StoreWrite(path, value, strict);
        }

        public readonly List<string> StoreDirectory(string path, bool strict = false) {
            return Active.StoreDirectory(path, strict);
        }

        public readonly void StoreRemove(string path, bool strict = false) {
            Active.StoreRemove(path, strict);
        }

        public readonly XenIfaceWatch WatchAdd(string path, bool strict = false) {
            var active = Active;
            AutoResetEvent? evt = null;
            XenIfaceWatchImpl? result = null;
            try {
                evt = new AutoResetEvent(false);
                result = new XenIfaceWatchImpl(path, strict, _parent!, evt, active);
                evt = null;
                _parent!._watches.Add(result);
                return result;
            } catch {
                result?.Dispose();
                evt?.Dispose();
                throw;
            }
        }
    }
}
