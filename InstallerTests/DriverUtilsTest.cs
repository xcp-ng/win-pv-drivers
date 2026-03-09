using XenDriverUtils;
using Xunit;

namespace InstallerTests {
    public class DriverUtilsTest {
        [Fact]
        public void TestParseMultiStringEmpty() {
            var buf = new char[] { };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Empty(result);
        }

        [Fact]
        public void TestParseMultiStringEmpty1() {
            var buf = new char[] { '\0' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Empty(result);
        }

        [Fact]
        public void TestParseMultiStringEmpty2() {
            var buf = new char[] { '\0', '\0' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Empty(result);
        }

        [Fact]
        public void TestParseMultiStringNormal() {
            var buf = new char[] { 'a', '\0', 'b', '\0', '\0' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Equal(2, result.Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b", result[1]);
        }

        [Fact]
        public void TestParseMultiStringBadSingle() {
            var buf = new char[] { 'a', 'b', 'c', '\0' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Single(result);
            Assert.Equal("abc", result[0]);
        }

        [Fact]
        public void TestParseMultiStringBadMultiple() {
            var buf = new char[] { 'a', '\0', 'b', 'c', '\0', 'd', '\0' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Equal(3, result.Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("bc", result[1]);
            Assert.Equal("d", result[2]);
        }

        [Fact]
        public void TestParseMultiStringNoTerminator() {
            var buf = new char[] { 'a', '\0', 'b', '\0', 'x', 'y' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Equal(3, result.Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b", result[1]);
            Assert.Equal("xy", result[2]);
        }

        [Fact]
        public void TestParseMultiStringTrailingGarbage() {
            var buf = new char[] { 'a', '\0', 'b', '\0', '\0', 'x', 'y' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Equal(2, result.Count);
            Assert.Equal("a", result[0]);
            Assert.Equal("b", result[1]);
        }

        [Fact]
        public void TestParseMultiStringLeadingNull() {
            var buf = new char[] { '\0', 'a', '\0', 'b', '\0', '\0' };
            var result = DriverUtils.ParseMultiString(buf);
            Assert.Empty(result);
        }
    }
}
