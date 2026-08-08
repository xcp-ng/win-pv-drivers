using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace XenPlus;

sealed class Program {
    [STAThread]
    static int Main() {
        using var single = new SingleInstance("{B27A618B-BF63-4DE1-894A-D3A696402174}");
        if (!single.IsTaken) {
            return 0;
        }

        return MessageLoopSynchronizationContext.Run(syncContext => {
            var mainWindow = new MainWindow();
            syncContext.Posted += (o, e) => mainWindow.OnPosted();
            mainWindow.Dispatched += (o, e) => syncContext.Dispatch();
            return mainWindow;
        });
    }
}
