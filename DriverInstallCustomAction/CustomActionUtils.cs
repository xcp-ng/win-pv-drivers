using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
    internal static class CustomActionUtils {
        public static MessageResult ReportActionData1(Session session, string actionData) {
            using var data = new Record(1);
            data[1] = actionData ?? string.Empty;
            return session.Message(InstallMessage.ActionData, data);
        }
    }
}
