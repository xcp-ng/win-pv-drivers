namespace XenPlus.XenIface;

[Serializable]
sealed class XenIfaceNotFoundException : Exception {
    public XenIfaceNotFoundException() {
    }

    public XenIfaceNotFoundException(string? message) : base(message) {
    }

    public XenIfaceNotFoundException(string? message, Exception? innerException) : base(message, innerException) {
    }
}
