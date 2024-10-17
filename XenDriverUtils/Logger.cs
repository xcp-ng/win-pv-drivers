namespace XenDriverUtils {
    public abstract class Logger {
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
}
