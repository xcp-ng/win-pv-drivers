using System.Text;

namespace XenPlus;

public class XenIfaceDeviceAbiTests {
    [Fact]
    public void RentStoreBufferIsCleared() {
        using var buffer = XenIface.XenIfaceDevice.RentStoreBuffer();

        foreach (var value in buffer.Span) {
            Assert.Equal(0, value);
        }
    }

    [Fact]
    public void FormatStringWritesTerminatedPermissiveValue() {
        byte[] buffer = [0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc];

        var length = XenIface.XenIfaceDevice.FormatString("A\u001f\u007f\n", buffer, false);

        Assert.Equal(4, length);
        Assert.Equal([(byte)'A', (byte)'?', 0x7f, (byte)'\n', 0, 0xcc], buffer);
    }

    [Fact]
    public void FormatStringWritesNullAsEmptyString() {
        byte[] buffer = [0xcc, 0xcc];

        var length = XenIface.XenIfaceDevice.FormatString(null, buffer, false);

        Assert.Equal(0, length);
        Assert.Equal([0, 0xcc], buffer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormatStringReservesSpaceForTerminator(bool strict) {
        byte[] buffer = [0xcc, 0xcc];

        var exception = Assert.Throws<ArgumentException>(
            () => XenIface.XenIfaceDevice.FormatString("AB", buffer, strict));

        Assert.Equal("value", exception.ParamName);
        Assert.Equal([0xcc, 0xcc], buffer);
    }

    [Fact]
    public void FormatStringStrictlyRejectsInvalidCharacters() {
        Assert.Throws<EncoderFallbackException>(
            () => XenIface.XenIfaceDevice.FormatString("\0", new byte[2], true));
    }

    [Fact]
    public void FormatPathWritesValidatedTerminatedPath() {
        byte[] buffer = [0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc];

        var length = XenIface.XenIfaceDevice.FormatPath("a/B-1", buffer);

        Assert.Equal(5, length);
        Assert.Equal([(byte)'a', (byte)'/', (byte)'B', (byte)'-', (byte)'1', 0], buffer);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a/")]
    [InlineData("a//b")]
    [InlineData("a\u0080b")]
    public void FormatPathRejectsInvalidPaths(string path) {
        Assert.Throws<ArgumentException>(
            () => XenIface.XenIfaceDevice.FormatPath(path, new byte[32]));
    }
}
