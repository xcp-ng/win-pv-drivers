using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
    internal static class CustomActionUtils {
        public static MessageResult ReportAction(Session session, string actionName, string message) {
            using var action = new Record(2);
            action[1] = actionName;
            action[2] = message;
            return session.Message(InstallMessage.ActionStart, action);
        }
    }
}
