using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace XenPlus;

/// <summary>Owns native IOCTL buffers and OVERLAPPED state until completion.</summary>
sealed unsafe class PendingIoctl : IDisposable {
    SafeHandleReferenceScope? _deviceReference;
    EventWaitHandle? _event;
    nint _input;
    nint _output;
    NativeOverlapped* _overlapped;
    bool _terminal;
    bool _disposed;

    public bool ImmediateSuccess { get; }
    public bool IsPending { get; }
    public bool IsTerminal => _terminal;
    public int ImmediateError { get; }
    public bool TerminalSuccess { get; private set; }
    public int TerminalError { get; private set; }

    public PendingIoctl(
        SafeFileHandle device,
        uint ioctl,
        int inputLength,
        int outputLength,
        Action<nint>? writeInput,
        Action<nint>? writeOutput = null) {
        ArgumentOutOfRangeException.ThrowIfNegative(inputLength);
        ArgumentOutOfRangeException.ThrowIfNegative(outputLength);
        try {
            _deviceReference = device.Borrow();
            _input = Allocate(inputLength);
            _output = Allocate(outputLength);
            _event = new EventWaitHandle(false, EventResetMode.ManualReset);
            // These requests use event completion, so we need to prevent .NET from receiving their complations via its
            // IOCP. If .NET does so, it may receive a completion after we have freed the OVERLAPPED struct.
            // Setting the low bit suppresses IOCP packets, including on opened or duplicated handles.
            _overlapped = new Overlapped {
                EventHandleIntPtr = _event.SafeWaitHandle.DangerousGetHandle() | 1
            }.Pack(null, null);
            writeInput?.Invoke(_input);
            writeOutput?.Invoke(_output);

            var rawDevice = new HANDLE(_deviceReference.Handle);
            var completed = PInvokeT.DeviceIoControl(
                rawDevice,
                ioctl,
                (void*)_input,
                (uint)inputLength,
                (void*)_output,
                (uint)outputLength,
                null,
                _overlapped);
            if (completed) {
                ImmediateSuccess = true;
                _terminal = true;
                TerminalSuccess = true;
                ReleaseDeviceReference();
            } else {
                ImmediateError = Marshal.GetLastPInvokeError();
                IsPending = ImmediateError == (int)WIN32_ERROR.ERROR_IO_PENDING;
                if (!IsPending) {
                    _terminal = true;
                    TerminalError = ImmediateError;
                    ReleaseDeviceReference();
                }
            }
        } catch {
            FreeBuffers();
            ReleaseDeviceReference();
            throw;
        }
    }

    static nint Allocate(int length) {
        if (length == 0) {
            return 0;
        }
        var pointer = Marshal.AllocHGlobal(length);
        unsafe {
            new Span<byte>((void*)pointer, length).Clear();
        }
        return pointer;
    }

    public int ReadInt32(int offset = 0) {
        return Marshal.ReadInt32(_output, offset);
    }

    public nint ReadIntPtr(int offset = 0) {
        return Marshal.ReadIntPtr(_output, offset);
    }

    public string ReadAsciiString(int maximumLength) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        var bytes = new byte[maximumLength];
        for (var index = 0; index < maximumLength; index++) {
            var value = Marshal.ReadByte(_output, index);
            if (value == 0) {
                return Encoding.ASCII.GetString(bytes, 0, index);
            }
            bytes[index] = value;
        }
        throw new InvalidDataException($"IOCTL output was not NUL-terminated within {maximumLength} bytes");
    }

    public bool WaitTerminal(TimeSpan timeout) {
        if (_terminal) {
            return true;
        }
        if (!_event!.WaitOne(timeout)) {
            return false;
        }

        uint bytesReturned = 0;
        var rawDevice = new HANDLE(_deviceReference!.Handle);
        var completed = PInvokeT.GetOverlappedResult(rawDevice, _overlapped, &bytesReturned, false);
        var error = completed ? 0 : Marshal.GetLastPInvokeError();
        if (!completed && error is (int)WIN32_ERROR.ERROR_IO_PENDING or (int)WIN32_ERROR.ERROR_IO_INCOMPLETE) {
            return false;
        }

        _terminal = true;
        TerminalSuccess = completed;
        TerminalError = error;
        ReleaseDeviceReference();
        return true;
    }

    public bool CancelAndWait(TimeSpan timeout) {
        if (_terminal) {
            return true;
        }
        var rawDevice = new HANDLE(_deviceReference!.Handle);
        if (!PInvokeT.CancelIoEx(rawDevice, _overlapped)) {
            var error = Marshal.GetLastPInvokeError();
            if (error != (int)WIN32_ERROR.ERROR_NOT_FOUND) {
                throw new Win32Exception(error, "CancelIoEx failed");
            }
        }
        return WaitTerminal(timeout);
    }

    void ReleaseDeviceReference() {
        _deviceReference?.Dispose();
        _deviceReference = null;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, true)) {
            return;
        }

        if (!_terminal) {
            // Native I/O may still reference these buffers; stop the child if the driver cannot reach a terminal result.
            var completionTimeout = XenIfaceIoctlTestSupport.CompletionTimeout;
            try {
                if (!CancelAndWait(completionTimeout)) {
                    Environment.FailFast($"IOCTL did not reach terminal completion within {completionTimeout}");
                }
            } catch (Exception error) {
                Environment.FailFast("Could not safely finish a pending IOCTL", error);
            }
        }

        FreeBuffers();
    }

    void FreeBuffers() {
        if (_input != 0) {
            Marshal.FreeHGlobal(_input);
            _input = 0;
        }
        if (_output != 0) {
            Marshal.FreeHGlobal(_output);
            _output = 0;
        }
        if (_overlapped != null) {
            Overlapped.Free(_overlapped);
            _overlapped = null;
        }
        _event?.Dispose();
        _event = null;
    }
}
