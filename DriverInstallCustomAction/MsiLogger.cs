using System;
using WixToolset.Dtf.WindowsInstaller;
using XenDriverUtils;

namespace XenInstCA {
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
