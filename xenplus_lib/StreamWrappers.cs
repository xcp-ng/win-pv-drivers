namespace XenPlus;

/// <param name="fatal">If false, returns no data when limit reached; if true, throws an exception</param>
/// <remarks><see cref="StreamReadLimiter"/> does not close the underlying stream.</remarks>
public sealed class StreamReadLimiter(Stream s, int limit, bool fatal = false) : Stream {
    readonly SemaphoreSlim _lock = new(1, 1);

    public override bool CanRead => s.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() {
        s.Flush();
    }

    int ApplyLimit(int count) {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 0, nameof(count));
        if (count == 0) {
            return 0;
        }
        if (limit <= 0) {
            if (fatal) {
                throw new InvalidOperationException("read limit reached");
            } else {
                return 0;
            }
        }
        return Math.Min(count, limit);
    }

    public override int Read(byte[] buffer, int offset, int count) {
        using var scope = _lock.EnterScope();
        var max = ApplyLimit(count);
        var bytesRead = s.Read(buffer, offset, max);
        limit -= bytesRead;
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) {
        _lock.Wait();
        try {
            var max = ApplyLimit(count);
            return s.BeginRead(buffer, offset, max, callback, state);
        } catch {
            _lock.Release();
            throw;
        }
    }

    public override int EndRead(IAsyncResult asyncResult) {
        try {
            var bytesRead = s.EndRead(asyncResult);
            limit -= bytesRead;
            return bytesRead;
        } finally {
            _lock.Release();
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        using var scope = await _lock.EnterScopeAsync(cancellationToken);
        var max = ApplyLimit(count);
        var bytesRead = await s.ReadAsync(buffer, offset, max, cancellationToken);
        limit -= bytesRead;
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        using var scope = await _lock.EnterScopeAsync(cancellationToken);
        var max = ApplyLimit(buffer.Length);
        var bytesRead = await s.ReadAsync(buffer[..max], cancellationToken);
        limit -= bytesRead;
        return bytesRead;
    }
}
