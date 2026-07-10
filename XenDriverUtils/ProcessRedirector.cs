using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XenDriverUtils {
    public class ProcessRedirector : IDisposable {
        readonly StreamReader _source;
        readonly CancellationToken _ct;
        readonly Task _task;
        readonly LogLevel _level;

        /// <remarks>This class takes ownership of <paramref name="source"/>.</remarks>
        public ProcessRedirector(StreamReader source, LogLevel level, CancellationToken ct = default) {
            _source = source;
            _ct = ct;
            _level = level;
            _task = Task.Run(() => Redirect());
        }

        public void Dispose() {
            _task.Wait(5000);
            if (!_task.IsCompleted) {
                Logger.Log(LogLevel.Alert, "ProcessRedirector task stuck");
            }
            _source.Dispose();
        }

        void Redirect() {
            while (!_ct.IsCancellationRequested) {
                try {
                    var line = _source.ReadLine();
                    if (line == null) {
                        break;
                    }
                    Logger.Log(_level, line);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }

        public static void LogProcessOutputs(Process process, TimeSpan? timeout, LogLevel level) {
            var cts = new CancellationTokenSource();
            using var outputRedir = new ProcessRedirector(process.StandardOutput, level, cts.Token);
            using var errorRedir = new ProcessRedirector(process.StandardError, level, cts.Token);
            if (timeout != null) {
                process.WaitForExit((int)timeout.Value.TotalMilliseconds);
                if (!process.HasExited) {
                    process.Kill();
                    Logger.LogFormat(LogLevel.Alert, "Process {0} timed out", process.Id);
                }
            } else {
                process.WaitForExit();
            }
            Logger.LogFormat(LogLevel.Info, "Process {0} exited with code {1}", process.Id, process.ExitCode);
            cts.Cancel();
        }

        public static Process LogCommand(string program, string args, TimeSpan? timeout, LogLevel level) {
            var psi = new ProcessStartInfo() {
                FileName = program,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var _ = new LogSection($"{program} {args}");
            var process = Process.Start(psi) ?? throw new NullReferenceException();
            process.StandardInput.Close();

            LogProcessOutputs(process, timeout, level);
            return process;
        }
    }
}
