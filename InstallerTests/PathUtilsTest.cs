using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using XenDriverUtils;
using Xunit;

namespace InstallerTests {
    public class PathUtilsTest {
        static void AssertOnlyTrustedFullControl(FileSystemSecurity security, bool expectInherited) {
            const int GenericAll = 0x10000000;
            using var identity = WindowsIdentity.GetCurrent();
            var allowed = new IdentityReference[] {
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                identity.User!,
            };

            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .ToList();
            Assert.NotEmpty(rules);
            foreach (var rule in rules) {
                Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
                Assert.Contains(rule.IdentityReference, allowed);
                // OICI ACEs with generic rights may be split into a mapped effective ACE and an inherit-only GENERIC_ALL ACE
                Assert.True(
                    (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl ||
                    ((int)rule.FileSystemRights & GenericAll) != 0,
                    $"Unexpected rights {rule.FileSystemRights} for {rule.IdentityReference}");
                Assert.Equal(expectInherited, rule.IsInherited);
            }
        }

        [Fact]
        public void TestSecureTempDirectoryAcl() {
            var dir = PathUtils.CreateSecureTempDirectory();
            try {
                var security = dir.GetAccessControl();
                Assert.True(security.AreAccessRulesProtected);
                AssertOnlyTrustedFullControl(security, expectInherited: false);

                var filePath = Path.Combine(dir.FullName, "child.txt");
                File.WriteAllText(filePath, "test");
                AssertOnlyTrustedFullControl(File.GetAccessControl(filePath), expectInherited: true);
            } finally {
                dir.Delete(true);
            }
        }
    }
}
