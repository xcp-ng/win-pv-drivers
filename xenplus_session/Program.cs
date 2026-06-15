using System.Diagnostics;
using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace XenPlus;

sealed class Program {
    [STAThread]
    static int Main() {
        var traceLogPath = Path.GetTempFileName();
        try {
            Trace.Listeners.Add(new TextWriterTraceListener(traceLogPath));

            using var single = new SingleInstance("{B27A618B-BF63-4DE1-894A-D3A696402174}");
            if (!single.IsTaken) {
                return 0;
            }

            using var mainWindow = new MainWindow();
            return MessageLoopSynchronizationContext.Run();
        } finally {
            File.Delete(traceLogPath);
        }
    }
}
