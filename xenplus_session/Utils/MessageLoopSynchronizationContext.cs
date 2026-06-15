using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace XenPlus;

class MessageLoopSynchronizationContext : SynchronizationContext, IDisposable {
    record WorkItem(SendOrPostCallback Callback, object? State, ExecutionContext? Context);

    readonly Thread _owner = Thread.CurrentThread;
    readonly ConcurrentQueue<WorkItem> _queue = new();
    readonly AutoResetEvent _pending = new(false);
    volatile bool _exited = false;

    public override void Post(SendOrPostCallback d, object? state) {
        _queue.Enqueue(new(d, state, ExecutionContext.Capture()));
        _pending.Set();
    }

    public override void Send(SendOrPostCallback d, object? state) {
        if (Thread.CurrentThread == _owner) {
            d(state);
            return;
        }

        using var wait = new ManualResetEventSlim();
        Exception? dex = null;
        Post(state => {
            try {
                d(state);
            } catch (Exception ex) {
                dex = ex;
            } finally {
                wait.Set();
            }
        }, state);
        wait.Wait();

        if (dex != null) {
            ExceptionDispatchInfo.Throw(dex);
        }
    }

    public override int Wait(nint[] waitHandles, bool waitAll, int millisecondsTimeout) {
        return base.Wait(waitHandles, waitAll, millisecondsTimeout);
    }

    public override SynchronizationContext CreateCopy() {
        return this;
    }

    int? DoWorkOne() {
        while (_queue.TryDequeue(out var item)) {
            if (item.Context != null) {
                ExecutionContext.Run(item.Context, (state) => item.Callback(state), item.State);
            } else {
                item.Callback(item.State);
            }
        }

        while (PInvoke.PeekMessage(out var msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE)) {
            if (msg.message == PInvoke.WM_QUIT) {
                Interlocked.Exchange(ref _exited, true);
                return (int)msg.wParam.Value;
            }
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        return null;
    }

    public static int Run() {
        using var context = new MessageLoopSynchronizationContext();
        var waiting = new HANDLE[1];

        SetSynchronizationContext(context);
        try {
            while (true) {
                WAIT_EVENT result;
                using (var shref = context._pending.SafeWaitHandle.Refer()) {
                    waiting[0] = (HANDLE)shref.DangerousHandle;
                    result = PInvoke.MsgWaitForMultipleObjects(
                       waiting,
                       false,
                       PInvoke.INFINITE,
                       QUEUE_STATUS_FLAGS.QS_ALLINPUT);
                }

                switch ((uint)result) {
                    case (uint)WAIT_EVENT.WAIT_OBJECT_0:
                    case (uint)WAIT_EVENT.WAIT_OBJECT_0 + 1:
                        if (context.DoWorkOne() is int exitCode) {
                            return exitCode;
                        }
                        break;
                    case (uint)WAIT_EVENT.WAIT_TIMEOUT:
                        throw new TimeoutException();
                    case (uint)WAIT_EVENT.WAIT_FAILED:
                        throw new Win32Exception(nameof(PInvoke.MsgWaitForMultipleObjects));
                    default:
                        throw new Exception($"Unexpected wait result {(uint)result}");
                }
            }
        } finally {
            SetSynchronizationContext(null);
        }
    }

    public void Dispose() {
        if (!_exited) {
            throw new InvalidOperationException("Message loop is still running");
        }
        _pending.Dispose();
    }
}
