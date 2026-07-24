namespace XenPlus;

public class ArrayPoolLeaseTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(4096)]
    public void RentExactExposesRequestedLength(int length) {
        using var lease = ArrayPoolLease<byte>.RentExact(length);

        Assert.Equal(length, lease.Span.Length);
        Assert.Equal(length, lease.Memory.Length);
        Assert.True(lease.Array.Length >= length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    public void RentExposesFullRentedArray(int minimumLength) {
        using var lease = ArrayPoolLease<byte>.Rent(minimumLength);

        Assert.True(lease.Array.Length >= minimumLength);
        Assert.Equal(lease.Array.Length, lease.Span.Length);
        Assert.Equal(lease.Array.Length, lease.Memory.Length);
    }

    [Fact]
    public void ViewsAndIndexersShareStorage() {
        using var lease = ArrayPoolLease<int>.RentExact(3);

        lease[0] = 10;
        lease[^1] = 30;
        lease.Memory.Span[1] = 20;

        Assert.Equal([10, 20, 30], lease.Span.ToArray());
        Assert.Equal(30, lease.Array[2]);
    }

    [Fact]
    public void IndexerRejectsOutOfRangeIndices() {
        using var lease = ArrayPoolLease<int>.RentExact(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = lease[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = lease[2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = lease[^0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = lease[^3]);
    }

    [Fact]
    public void AccessAfterDisposeThrows() {
        var lease = ArrayPoolLease<byte>.RentExact(1);
        lease.Dispose();
        lease.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = lease.Array);
        Assert.Throws<ObjectDisposedException>(() => {
            _ = lease.Span;
        });
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Memory);
        Assert.Throws<ObjectDisposedException>(() => _ = lease[0]);
    }
}
