using System.Text;

namespace XenPlus;

public class StoreEncodingTests {
    static Encoding GetEncoding(bool strict) => strict ?
        XenIface.StrictStoreEncoding.Instance :
        XenIface.StoreEncoding.Instance;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public unsafe void PointerOverloadsRejectNullEmptyBuffers(bool strict) {
        var encoding = GetEncoding(strict);

        Assert.ThrowsAny<Exception>(() => encoding.GetByteCount((char*)null, 0));
        Assert.ThrowsAny<Exception>(() => {
            char input = default;
            encoding.GetBytes(&input, 0, (byte*)null, 0);
        });
        Assert.ThrowsAny<Exception>(() => encoding.GetCharCount((byte*)null, 0));
        Assert.ThrowsAny<Exception>(() => {
            byte input = default;
            encoding.GetChars(&input, 0, (char*)null, 0);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpanOverloadsAcceptEmptyBuffers(bool strict) {
        var encoding = GetEncoding(strict);

        Assert.Equal(0, encoding.GetByteCount([]));
        Assert.Equal(0, encoding.GetBytes([], []));
        Assert.Equal(0, encoding.GetCharCount([]));
        Assert.Equal(0, encoding.GetChars([], []));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidStoreCharactersRoundTrip(bool strict) {
        var encoding = GetEncoding(strict);
        const string value = "\n AZaz09~\u007f";

        var bytes = encoding.GetBytes(value);

        Assert.Equal(value.Select(c => (byte)c), bytes);
        Assert.Equal(value, encoding.GetString(bytes));
    }

    [Fact]
    public void PermissiveEncodingReplacesInvalidCharactersAndBytes() {
        var encoding = XenIface.StoreEncoding.Instance;

        Assert.Equal(
            [(byte)'?', (byte)'?', (byte)'?', (byte)'?'],
            encoding.GetBytes("\0\u001f\u0080\u20ac"));
        Assert.Equal(
            "????",
            encoding.GetString([0x00, 0x1f, 0x80, 0xff]));
    }

    [Fact]
    public void StrictEncodingRejectsInvalidCharactersAndBytes() {
        var encoding = XenIface.StrictStoreEncoding.Instance;

        Assert.Throws<EncoderFallbackException>(() => encoding.GetBytes("\0"));
        Assert.Throws<EncoderFallbackException>(() => encoding.GetBytes("\u0080"));
        Assert.Throws<DecoderFallbackException>(() => encoding.GetString([0x00]));
        Assert.Throws<DecoderFallbackException>(() => encoding.GetString([0x80]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArrayOverloadsHonorInputAndOutputOffsets(bool strict) {
        var encoding = GetEncoding(strict);
        char[] input = ['x', 'A', '\n', 'B', 'y'];
        byte[] bytes = [0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc];

        var bytesWritten = encoding.GetBytes(input, 1, 3, bytes, 2);

        Assert.Equal(3, bytesWritten);
        Assert.Equal([0xcc, 0xcc, (byte)'A', (byte)'\n', (byte)'B', 0xcc], bytes);

        char[] output = ['x', 'x', 'x', 'x', 'x'];
        var charsWritten = encoding.GetChars(bytes, 2, 3, output, 1);

        Assert.Equal(3, charsWritten);
        Assert.Equal(['x', 'A', '\n', 'B', 'x'], output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpanOverloadsRejectUndersizedDestinations(bool strict) {
        var encoding = GetEncoding(strict);

        Assert.ThrowsAny<ArgumentException>(
            () => encoding.GetBytes("AB".AsSpan(), new byte[1]));
        Assert.ThrowsAny<ArgumentException>(
            () => encoding.GetChars([(byte)'A', (byte)'B'], new char[1]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaximumCountsMatchSingleByteEncoding(bool strict) {
        var encoding = GetEncoding(strict);

        Assert.Equal(123, encoding.GetMaxByteCount(123));
        Assert.Equal(123, encoding.GetMaxCharCount(123));
        Assert.Throws<ArgumentOutOfRangeException>(() => encoding.GetMaxByteCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => encoding.GetMaxCharCount(-1));
    }
}
