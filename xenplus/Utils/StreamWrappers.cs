namespace XenPlus;

/// <param name="fatal">
/// If false, throws <see cref="EndOfStreamException"/> when limit reached; if true, throws
/// something else
/// </param>
/// <remarks><see cref="StreamReadLimiter"/> does not close the underlying stream.</remarks>
sealed class StreamReadLimiter(Stream s, int limit, bool fatal = false) : Stream {
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
        if (count == 0) {
            return 0;
        }
        if (limit <= 0) {
            if (fatal) {
                throw new InvalidOperationException("read limit reached");
            } else {
                throw new EndOfStreamException("read limit reached");
            }
        }
        return Math.Min(count, limit);
    }

    public override int Read(byte[] buffer, int offset, int count) {
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
        var max = ApplyLimit(count);
        return s.BeginRead(buffer, offset, max, callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult) {
        var bytesRead = s.EndRead(asyncResult);
        limit -= bytesRead;
        return bytesRead;
    }
}
