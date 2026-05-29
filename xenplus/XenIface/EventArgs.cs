namespace XenPlus.XenIface;

public sealed class XenIfaceWatchEventArgs(string path) {
    public string Path => path;
}

public sealed class XenIfaceResumedEventArgs : EventArgs {
}
