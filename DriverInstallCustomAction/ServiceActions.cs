using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.System.Services;
using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
    public class ServiceActions {
        [CustomAction]
        public static ActionResult ConfigureXenplusService(Session session) {
            using var logScope = new LoggerScope(new MsiSessionLogger(session));
            if (!session.CustomActionData.TryGetValue("Service", out var serviceName))
                return ActionResult.Success;

            using var scm = PInvoke.OpenSCManager((string)null, null, PInvoke.SC_MANAGER_ALL_ACCESS);
            if (scm.IsInvalid) {
                throw new Win32Exception();
            }

            using var service = PInvoke.OpenService(scm, serviceName, PInvoke.SERVICE_ALL_ACCESS);
            if (service.IsInvalid) {
                throw new Win32Exception();
            }

            unsafe {
                var flag = new SERVICE_FAILURE_ACTIONS_FLAG() { fFailureActionsOnNonCrashFailures = true };
                if (!PInvoke.ChangeServiceConfig2W(service, SERVICE_CONFIG.SERVICE_CONFIG_FAILURE_ACTIONS_FLAG, &flag)) {
                    throw new Win32Exception();
                }
            }

            return ActionResult.Success;
        }
    }
}
