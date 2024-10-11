using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
    public static class RebootActions {
        [CustomAction]
        public static ActionResult CheckReboot(Session session) {
            if (CustomActionUtils.IsRebootScheduled()) {
                session.SetMode(InstallRunMode.RebootAtEnd, true);
            }
            return ActionResult.Success;
        }
    }
}
