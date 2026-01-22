using System;

namespace XenDriverUtils {
    public class LogSection : IDisposable {
        readonly string _name;

        public LogSection(string name) {
            _name = name;
            Logger.LogFormat(LogLevel.Info, "***** begin {0} *****", _name);
        }

        public void Dispose() {
            Logger.LogFormat(LogLevel.Info, "***** end {0} *****", _name);
        }
    }
}
