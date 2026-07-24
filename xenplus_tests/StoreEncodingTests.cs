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
}
