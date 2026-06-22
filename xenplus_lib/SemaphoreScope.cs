namespace XenPlus;

public sealed class SemaphoreScope(SemaphoreSlim _sem) : IDisposable {
    bool _disposed = false;

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }
        try {
            _sem.Release();
        } catch (Exception ex) {
            Environment.FailFast("SemaphoreScope release failed", ex);
        }
    }
}

public static class SemaphoreExtensions {
    public static SemaphoreScope EnterScope(this SemaphoreSlim sem) {
        sem.Wait();
        return new(sem);
    }

    public static async Task<SemaphoreScope> EnterScopeAsync(
        this SemaphoreSlim sem,
        CancellationToken cancellationToken = default) {
        await sem.WaitAsync(cancellationToken);
        return new(sem);
    }
}
