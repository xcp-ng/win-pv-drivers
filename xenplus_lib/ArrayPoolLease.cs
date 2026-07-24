using System.Buffers;

namespace XenPlus;

public sealed class ArrayPoolLease<T> : IDisposable {
    readonly ArrayPool<T> _pool;
    readonly bool _clearArray;
    T[]? _array;

    ArrayPoolLease(ArrayPool<T> pool, int minimumLength, bool clearArray) {
        _pool = pool;
        _clearArray = clearArray;
        _array = pool.Rent(minimumLength);
    }

    public T[] Array => _array ?? throw new ObjectDisposedException(nameof(ArrayPoolLease<>));
    public Span<T> Span => Array;
    public Memory<T> Memory => Array;

    public ref T this[int index] => ref Array[index];

    public ref T this[Index index] => ref Array[index];

    public static ArrayPoolLease<T> Rent(int minimumLength, bool clearArray = false) {
        return new(ArrayPool<T>.Shared, minimumLength, clearArray);
    }

    public void Dispose() {
        var array = Interlocked.Exchange(ref _array, null);
        if (array != null) {
            _pool.Return(array, _clearArray);
        }
    }
}
