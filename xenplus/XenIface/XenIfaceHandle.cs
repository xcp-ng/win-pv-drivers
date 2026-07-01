namespace XenPlus.XenIface;

/// <summary>
/// Lock handle of a <see cref="XenIfaceSource"/> object.
/// </summary>
internal readonly ref struct XenIfaceHandle(XenIfaceSource parent) : IDisposable {
    /// <remarks>
    /// <para>
    /// Both <c>class</c> and <c>ref struct</c> are inappropriate for <see cref="XenIfaceHandle"/> alone, the former
    /// because the handle can escape its scope/thread (and therefore violate the thread requirements of
    /// <see cref="Monitor"/>), and the latter because the handle can be copied. So we use a <c>class</c> wrapped by
    /// a <c>ref struct</c> to provide both guarantees.
    /// </para>
    /// <para>
    /// Note that the inner class doesn't make the handle uncopyable, but rather copy-safe.
    /// </para>
    /// </remarks>
    class XenIfaceInternalHandle : IDisposable {
        public XenIfaceSource? Parent { get; private set; } = null;

        internal XenIfaceInternalHandle(XenIfaceSource parent) {
            try {
                Monitor.Enter(parent.SyncRoot);
                Parent = parent;
            } catch {
                Dispose();
                throw;
            }
        }

        public void Dispose() {
            if (Parent != null) {
                Monitor.Exit(Parent.SyncRoot);
                Parent = null;
            }
        }
    }

    XenIfaceDevice Active {
        get {
            ObjectDisposedException.ThrowIf(_h.Parent == null, typeof(XenIfaceHandle));
            return _h.Parent.Active ?? throw new XenIfaceNotFoundException("no active device");
        }
    }

    readonly XenIfaceInternalHandle _h = new(parent);

    public void Dispose() {
        _h.Dispose();
    }

    public string StoreRead(string path, bool strict = false) {
        return Active.StoreRead(path, strict);
    }

    public void StoreWrite(string path, string? value, bool strict = false) {
        Active.StoreWrite(path, value, strict);
    }

    public List<string> StoreDirectory(string path, bool strict = false) {
        return Active.StoreDirectory(path, strict);
    }

    public void StoreRemove(string path, bool strict = false) {
        Active.StoreRemove(path, strict);
    }

    public XenIfaceWatch WatchAdd(string path, bool strict = false) {
        var active = Active;
        return _h.Parent!.WatchAddLocked(active, path, strict);
    }
}
