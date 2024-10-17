using System;
using XenDriverUtils;

namespace XenClean {
    internal class ConsoleLogger : Logger {
        public ConsoleLogger() {
        }

        public override void Write(string message) {
            Console.WriteLine(message);
        }
    }
}
