using XenPlus.XenIface;

namespace XenPlus;

public class StoreUtilsTests {
    [Fact]
    public void PathJoinWithoutSuffixesPreservesRoot() {
        Assert.Equal("", StoreUtils.PathJoin(null));
        Assert.Equal("", StoreUtils.PathJoin(""));
        Assert.Equal("root", StoreUtils.PathJoin("root"));
        Assert.Equal("root/", StoreUtils.PathJoin("root/"));
    }

    [Fact]
    public void PathJoinAddsOneSeparatorBetweenComponents() {
        Assert.Equal("/a/b", StoreUtils.PathJoin(null, "a", "b"));
        Assert.Equal("root/a/b", StoreUtils.PathJoin("root", "a", "b"));
        Assert.Equal("root/a", StoreUtils.PathJoin("root/", "a"));
        Assert.Equal("/a", StoreUtils.PathJoin("/", "a"));
    }

    [Fact]
    public void PathJoinRejectsAbsoluteSuffix() {
        Assert.Throws<ArgumentException>(() => StoreUtils.PathJoin("root", "/absolute"));
        Assert.Throws<ArgumentException>(() => StoreUtils.PathJoin("root", "valid", "/absolute"));
    }

    [Fact]
    public void PathJoinRejectsNullSuffixCollectionOrElement() {
        Assert.Throws<ArgumentNullException>(() => StoreUtils.PathJoin("root", null!));
        Assert.Throws<ArgumentNullException>(() => StoreUtils.PathJoin("root", ["valid", null!]));
    }
}
