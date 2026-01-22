using System;
using System.IO;
using XenDriverUtils;

namespace XenClean {
    class TempFileLogger : Logger, IDisposable {
        readonly string _logPath;
        readonly TextWriter _writer;

        public string LogPath => _logPath;

        public TempFileLogger() {
            _logPath = Path.GetTempFileName();
            try {
                _writer = File.CreateText(_logPath);
            } catch (Exception ex) {
                _writer = null;
                WriteFormat(LogLevel.Alert, "Opening log path {0} failed: {1} {2}", _logPath, ex.HResult, ex.Message);
            }
        }

        public void Dispose() {
            _writer?.Dispose();
        }

        protected override void Write(LogLevel level, string message) {
            var toWrite = $"[{level}] " + message;
            if (level <= LogLevel.Interactive) {
                Console.WriteLine(toWrite);
            }
            if (_writer != null) {
                _writer.WriteLine(toWrite);
                if (level <= LogLevel.Interactive) {
                    _writer.Flush();
                }
            }
        }

        protected override void WriteFormat(LogLevel level, string format, params object[] args) {
            var toWrite = $"[{level}] " + string.Format(format, args);
            if (level <= LogLevel.Interactive) {
                Console.WriteLine(toWrite);
            }
            if (_writer != null) {
                _writer.WriteLine(toWrite);
                if (level <= LogLevel.Interactive) {
                    _writer.Flush();
                }
            }
        }
    }
}
