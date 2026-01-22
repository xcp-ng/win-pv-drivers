namespace XenDriverUtils {
    public enum LogLevel {
        Alert,
        Interactive,
        Info,
        Trace,
    }

    public abstract class Logger {
        protected abstract void Write(LogLevel level, string message);
        protected abstract void WriteFormat(LogLevel level, string format, params object[] args);

        private static Logger Instance = null;

        public static Logger SetLogger(Logger logger) {
            var old = Instance;
            Instance = logger;
            return old;
        }

        public static void Log(string message) {
            Instance?.Write(LogLevel.Interactive, message);
        }

        public static void Log(LogLevel level, string message) {
            Instance?.Write(level, message);
        }

        public static void LogFormat(LogLevel level, string format, params object[] args) {
            Instance?.WriteFormat(level, format, args);
        }
    }
}
