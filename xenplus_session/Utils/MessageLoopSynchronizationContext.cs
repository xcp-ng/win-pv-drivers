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
    readonly CancellationTokenSource _exited = new();
    readonly CancellationToken _ct;

    public MessageLoopSynchronizationContext() {
        _ct = _exited.Token;
    }

    /// <remarks>
    /// Invoked synchronously in <see cref="Post"/>, so handlers should queue work/post message instead of running them
    /// synchronously.
    /// </remarks>
    public event EventHandler? Posted;

    public override void Post(SendOrPostCallback d, object? state) {
        ObjectDisposedException.ThrowIf(_exited.IsCancellationRequested, this);
        _queue.Enqueue(new(d, state, ExecutionContext.Capture()));
        _pending.Set();
        Posted?.Invoke(this, EventArgs.Empty);
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
        wait.Wait(_ct);

        if (dex != null) {
            ExceptionDispatchInfo.Throw(dex);
        }
    }

    public override SynchronizationContext CreateCopy() {
        return this;
    }

    public void Dispatch() {
        while (_queue.TryDequeue(out var item)) {
            if (item.Context != null) {
                ExecutionContext.Run(item.Context, (state) => item.Callback(state), item.State);
            } else {
                item.Callback(item.State);
            }
        }
    }

    int? DoWorkOne() {
        Dispatch();

        while (PInvoke.PeekMessage(out var msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE)) {
            if (msg.message == PInvoke.WM_QUIT) {
                return (int)msg.wParam.Value;
            }
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        return null;
    }

    /// <remarks>
    /// <see cref="MainWindow"/> or whatever similar thing must outlive the <see cref="initializer"/>, so do not dispose
    /// it inside the initializer.
    /// </remarks>
    public static int Run(Func<MessageLoopSynchronizationContext, IDisposable> initializer) {
        using var context = new MessageLoopSynchronizationContext();
        var waiting = new HANDLE[1];

        var previous = Current;
        SetSynchronizationContext(context);
        try {
            IDisposable? lifetime = null;
            try {
                lifetime = initializer(context);

                while (true) {
                    WAIT_EVENT result;
                    using (var shref = context._pending.SafeWaitHandle.Borrow()) {
                        waiting[0] = (HANDLE)shref.Handle;
                        result = PInvoke.MsgWaitForMultipleObjects(
                           waiting,
                           false,
                           PInvoke.INFINITE,
                           QUEUE_STATUS_FLAGS.QS_ALLINPUT);
                    }

                    switch (result) {
                        case WAIT_EVENT.WAIT_OBJECT_0:
                        case WAIT_EVENT.WAIT_OBJECT_0 + 1:
                            if (context.DoWorkOne() is int exitCode) {
                                return exitCode;
                            }
                            break;
                        case WAIT_EVENT.WAIT_TIMEOUT:
                            throw new TimeoutException();
                        case WAIT_EVENT.WAIT_FAILED:
                            throw new Win32Exception(nameof(PInvoke.MsgWaitForMultipleObjects));
                        default:
                            throw new Exception($"Unexpected wait result {result}");
                    }
                }
            } finally {
                // since the initializer may have done work, we still need to cancel them even if it failed
                context._exited.Cancel();
                // it also implies that lifetime.Dispose() cannot post work
                lifetime?.Dispose();
            }
        } finally {
            SetSynchronizationContext(previous);
        }
    }

    public void Dispose() {
        if (!_exited.IsCancellationRequested) {
            throw new InvalidOperationException("Message loop is still running");
        }
        /// Don't dispose <see cref="_pending"/> since someone could still be in <see cref="Post"/>.
        /// Conversely, <see cref="_exited"/> can be disposed here since <see cref="Send"/> can survive that.
        _exited.Dispose();
    }
}
