using Microsoft.Win32;

namespace XenPlus;

public class PolicyStoreTests {
    [Fact]
    public void MissingKeyAndMissingValueRemainNull() {
        using var registry = new TestRegistry();
        var missing = registry.CreateRoot("Missing");
        var present = registry.CreateRoot("Present");

        using var policyKey = present.CreateSubKey(TestRegistry.PolicyPath);

        var store = new PolicyStore<TestPolicy>("TestVendor", "TestCategory", missing, present);

        Assert.Equal(2, store.Policies.Count);
        Assert.Null(store.Policies[0]);
        Assert.NotNull(store.Policies[1]);
        Assert.Null(store.Policies[1]!.Enabled);
        Assert.Null(store.Get(policy => policy.Enabled));
        Assert.Null(IPolicyInstance<TestPolicy>.ReadBool(policyKey, "Absent"));
    }

    [Fact]
    public void FailedKeyOpenAndValueReadAreTolerated() {
        using var registry = new TestRegistry();
        var closed = registry.CreateRoot("Closed");
        closed.Dispose();
        var present = registry.CreateRoot("Present");

        using var policyKey = present.CreateSubKey(TestRegistry.PolicyPath);
        policyKey.SetValue("Enabled", 1, RegistryValueKind.DWord);

        var store = new PolicyStore<TestPolicy>("TestVendor", "TestCategory", closed, present);

        Assert.Null(store.Policies[0]);
        Assert.True(store.Get(policy => policy.Enabled));
        Assert.Null(IPolicyInstance<TestPolicy>.ReadBool(closed, "Enabled"));
        policyKey.SetValue("Invalid", "true", RegistryValueKind.String);
        Assert.Null(IPolicyInstance<TestPolicy>.ReadBool(policyKey, "Invalid"));
    }

    [Fact]
    public void FirstNonNullValueWinsAndNullFallsThrough() {
        using var registry = new TestRegistry();
        var first = registry.CreateRoot("First");
        var second = registry.CreateRoot("Second");

        using var firstKey = first.CreateSubKey(TestRegistry.PolicyPath);
        using var secondKey = second.CreateSubKey(TestRegistry.PolicyPath);

        firstKey.SetValue("Enabled", 0, RegistryValueKind.DWord);
        secondKey.SetValue("Enabled", 1, RegistryValueKind.DWord);
        secondKey.SetValue("Name", "fallback", RegistryValueKind.String);

        var store = new PolicyStore<TestPolicy>("TestVendor", "TestCategory", first, second);

        Assert.False(store.Get(policy => policy.Enabled));
        Assert.Equal("fallback", store.Get(policy => policy.Name));
    }

    private sealed record TestPolicy(bool? Enabled, string? Name) : IPolicyInstance<TestPolicy> {
        public static TestPolicy LoadPolicy(RegistryKey key) => new(
            IPolicyInstance<TestPolicy>.ReadBool(key, "Enabled"),
            key.GetValue("Name") as string);
    }

    private sealed class TestRegistry : IDisposable {
        public const string PolicyPath = @"SOFTWARE\Policies\TestVendor\TestCategory";

        readonly string _path = $@"Software\XenPlusPolicyStoreTests\{Guid.NewGuid():N}";
        readonly List<RegistryKey> _roots = [];

        public RegistryKey CreateRoot(string name) {
            var root = Registry.CurrentUser.CreateSubKey($@"{_path}\{name}");
            _roots.Add(root);
            return root;
        }

        public void Dispose() {
            foreach (var root in _roots) {
                root.Dispose();
            }
            Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);
        }
    }
}
