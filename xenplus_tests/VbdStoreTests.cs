using XenPlus.VolumeInfo;

namespace XenPlus;

public class VbdStoreTests {
    [Theory]
    [InlineData(3u << 8, "hd", 0u)]
    [InlineData((3u << 8) | (1u << 6), "hd", 1u)]
    [InlineData(22u << 8, "hd", 2u)]
    [InlineData((22u << 8) | (1u << 6), "hd", 3u)]
    [InlineData(8u << 8, "sd", 0u)]
    [InlineData((8u << 8) | (15u << 4), "sd", 15u)]
    [InlineData(202u << 8, "xvd", 0u)]
    [InlineData((202u << 8) | (15u << 4), "xvd", 15u)]
    [InlineData(1u << 28, "xvd", 0u)]
    [InlineData((1u << 28) | (4095u << 8), "xvd", 4095u)]
    public void VbdNumberToDiskNumberSupportsFormats(uint vbdNumber, string expectedPrefix, uint expectedDiskNumber) {

        var result = VbdStore.VbdNumberToDiskNumber(vbdNumber);

        Assert.Equal((expectedPrefix, expectedDiskNumber), result);
    }

    [Theory]
    [InlineData((3u << 8) | 1u)]
    [InlineData((22u << 8) | 1u)]
    [InlineData((8u << 8) | 1u)]
    [InlineData((202u << 8) | 1u)]
    [InlineData((1u << 28) | 1u)]
    [InlineData((1u << 28) | (1u << 20))]
    public void VbdNumberToDiskNumberRejectsReservedBits(uint vbdNumber) {
        Assert.Throws<ArgumentOutOfRangeException>(() => VbdStore.VbdNumberToDiskNumber(vbdNumber));
    }

    [Fact]
    public void VbdNumberToDiskNumberRejectsUnsupportedFormat() {
        Assert.Throws<ArgumentException>(() => VbdStore.VbdNumberToDiskNumber(0));
    }

    [Theory]
    [InlineData(0u, "xvda")]
    [InlineData(25u, "xvdz")]
    [InlineData(26u, "xvdaa")]
    [InlineData(27u, "xvdab")]
    [InlineData(51u, "xvdaz")]
    [InlineData(52u, "xvdba")]
    [InlineData(701u, "xvdzz")]
    [InlineData(702u, "xvdaaa")]
    public void FormatVbdNameProvidesCorrectSuffix(uint id, string expected) {
        Assert.Equal(expected, VbdStore.FormatVbdName("xvd", id));
    }
}
