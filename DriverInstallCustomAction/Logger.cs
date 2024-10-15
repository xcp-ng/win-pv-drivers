using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WixToolset.Dtf.WindowsInstaller;

namespace XenInstCA {
    internal abstract class Logger {
        public abstract void Write(string message);

        private static Logger Instance = null;

        public static Logger SetLogger(Logger logger) {
            var old = Instance;
            Instance = logger;
            return old;
        }

        public static void Log(string message) {
            Instance?.Write(message);
        }
    }

    internal class MsiSessionLogger : Logger {
        private readonly Session _session;

        public MsiSessionLogger(Session session) {
            _session = session;
        }

        public override void Write(string message) {
            _session.Log(message);
        }
    }

    internal class LoggerScope : IDisposable {
        private bool disposedValue;
        private Logger _old;

        public LoggerScope(Logger logger) {
            _old = Logger.SetLogger(logger);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Logger.SetLogger(_old);
                    _old = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
