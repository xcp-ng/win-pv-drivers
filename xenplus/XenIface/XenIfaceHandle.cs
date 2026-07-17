namespace XenPlus.XenIface;

/// <summary>
/// Lock handle of a <see cref="XenIfaceSource"/> object.
/// </summary>
internal readonly ref struct XenIfaceHandle(XenIfaceSource parent) : IDisposable {
    /// <remarks>
    /// <para>
    /// Both <see langword="class"/> and <see langword="ref"/> <see langword="struct"/> are inappropriate for
    /// <see cref="XenIfaceHandle"/> alone, the former because the handle can escape its scope/thread (and therefore
    /// violate the thread requirements of <see cref="Monitor"/>), and the latter because the handle can be copied. So
    /// we use a <see langword="class"/> wrapped by a <see langword="ref"/> <see langword="struct"/> to provide both
    /// guarantees.
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

    public string StoreReadStrict(string path) {
        return Active.StoreRead(path, true);
    }

    public void StoreWrite(string path, string? value, bool strict = false) {
        Active.StoreWrite(path, value, strict);
    }

    public void StoreWriteStrict(string path, string? value) {
        Active.StoreWrite(path, value, true);
    }

    public List<string> StoreDirectory(string path) {
        return Active.StoreDirectory(path);
    }

    public void StoreRemove(string path) {
        Active.StoreRemove(path);
    }

    public XenIfaceWatch WatchAdd(string path) {
        var active = Active;
        return _h.Parent!.WatchAddLocked(active, path);
    }
}
