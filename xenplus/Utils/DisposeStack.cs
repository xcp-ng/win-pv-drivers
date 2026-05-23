namespace XenPlus;

sealed class DisposeStack : IDisposable {
    readonly Stack<IDisposable> _stack = new();

    public void Push(IDisposable item) {
        _stack.Push(item);
    }

    /// <summary>
    /// Dispose all pushed objects. You can still push later.
    /// </summary>
    public void Dispose() {
        while (_stack.TryPop(out var item)) {
            item?.Dispose();
        }
    }
}
