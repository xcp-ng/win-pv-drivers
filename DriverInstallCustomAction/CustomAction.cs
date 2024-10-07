using System;
using System.Collections.Generic;
using System.Linq;
using WixToolset.Dtf.WindowsInstaller;

namespace XNInstCA {
    public class CustomActions {
        static readonly List<string> ComponentList = new() {
            "Xenbus",
            "Xeniface",
            "Xencons",
            "Xenvkbd",
            "Xenhid",
            "Xenvif",
            "Xennet",
            "Xenvbd",
        };


        static KeyValuePair<string, string>? GetComponentInf(Session session) {
            foreach (var componentName in ComponentList) {
                if (session.CustomActionData.TryGetValue(componentName, out var componentInf) && !string.IsNullOrEmpty(componentInf)) {
                    return new KeyValuePair<string, string>(componentName, componentInf);
                }
            }

            return null;
        }

        [CustomAction]
        public static ActionResult FakeInstall(Session session) {
            var component = GetComponentInf(session);
            if (component != null) {
                session.Log($"Installing {component.Value.Key} inf {component.Value.Value}");
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeInstallRollback(Session session) {
            FakeUninstall(session);
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeUninstall(Session session) {
            var component = GetComponentInf(session);
            if (component != null) {
                session.Log($"Uninstalling {component.Value.Key} inf {component.Value.Value}");
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult FakeUninstallRollback(Session session) {
            // Don't roll back uninstall (i.e. reinstall)
            return ActionResult.Success;
        }
    }
}
