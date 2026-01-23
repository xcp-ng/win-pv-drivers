using System;
using XenDriverUtils;

namespace XenClean {
    class ConsoleLogger : Logger {
        public ConsoleLogger() {
        }

        protected override void Write(LogLevel level, string message) {
            if (level <= LogLevel.Interactive) {
                var toWrite = $"[{level}] " + message;
                Console.WriteLine(toWrite);
            }
        }

        protected override void WriteFormat(LogLevel level, string format, params object[] args) {
            if (level <= LogLevel.Interactive) {
                var toWrite = $"[{level}] " + string.Format(format, args);
                Console.WriteLine(toWrite);
            }
        }
    }
}
