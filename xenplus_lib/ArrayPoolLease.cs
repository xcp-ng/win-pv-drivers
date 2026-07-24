using System.Buffers;

namespace XenPlus;

public sealed class ArrayPoolLease<T> : IDisposable {
    readonly ArrayPool<T> _pool;
    readonly bool _clearArray;
    readonly int _length;
    T[]? _array;

    ArrayPoolLease(ArrayPool<T> pool, int minimumLength, bool clearArray, int? exactLength) {
        _pool = pool;
        _clearArray = clearArray;
        _array = pool.Rent(minimumLength);
        _length = exactLength ?? _array.Length;
    }

    /// <remarks>
    /// Returns the full rented array.
    /// If you need the exact-rented buffers, use the <see cref="Span"/> or <see cref="Memory"/> props instead.
    /// </remarks>
    public T[] Array => _array ?? throw new ObjectDisposedException(nameof(ArrayPoolLease<>));

    public Span<T> Span {
        get {
            var array = Array;
            return array.AsSpan(0, _length);
        }
    }

    public Memory<T> Memory {
        get {
            var array = Array;
            return array.AsMemory(0, _length);
        }
    }

    public ref T this[int index] {
        get {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _length);
            return ref Array[index];
        }
    }

    public ref T this[Index index] => ref this[index.GetOffset(_length)];

    public static ArrayPoolLease<T> Rent(int minimumLength, bool clearArray = false) {
        return new(ArrayPool<T>.Shared, minimumLength, clearArray, null);
    }

    public static ArrayPoolLease<T> RentExact(int length, bool clearArray = false) {
        return new(ArrayPool<T>.Shared, length, clearArray, length);
    }

    public void Dispose() {
        var array = Interlocked.Exchange(ref _array, null);
        if (array != null) {
            _pool.Return(array, _clearArray);
        }
    }
}
