namespace XenPlus;

public class DisposeStackTests {
    sealed class TrackedDisposable(string name, List<string> disposalOrder) : IDisposable {
        public int DisposeCount { get; private set; }

        public void Dispose() {
            DisposeCount++;
            disposalOrder.Add(name);
        }
    }

    [Fact]
    public void DisposeEmptyStackDoesNothing() {
        var stack = new DisposeStack();

        stack.Dispose();
        stack.Dispose();
    }

    [Fact]
    public void DisposeUsesLastInFirstOutOrder() {
        var disposalOrder = new List<string>();
        var first = new TrackedDisposable("first", disposalOrder);
        var second = new TrackedDisposable("second", disposalOrder);
        var third = new TrackedDisposable("third", disposalOrder);
        var stack = new DisposeStack();
        stack.Push(first);
        stack.Push(second);
        stack.Push(third);

        stack.Dispose();

        Assert.Equal(["third", "second", "first"], disposalOrder);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(1, third.DisposeCount);
    }

    [Fact]
    public void StackCanBeReusedAfterDisposal() {
        var disposalOrder = new List<string>();
        var first = new TrackedDisposable("first", disposalOrder);
        var second = new TrackedDisposable("second", disposalOrder);
        var stack = new DisposeStack();

        stack.Push(first);
        stack.Dispose();
        stack.Push(second);
        stack.Dispose();
        stack.Dispose();

        Assert.Equal(["first", "second"], disposalOrder);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }
}
