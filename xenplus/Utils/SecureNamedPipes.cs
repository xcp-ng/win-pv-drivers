using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Pipes;

namespace XenPlus;

static class SecureNamedPipes {
    public static NamedPipeServerStream Listen(
        string pipePath,
        PipeDirection direction,
        int maxNumberOfServerInstances,
        PipeTransmissionMode transmissionMode,
        PipeOptions options,
        int inBufferSize,
        int outBufferSize,
        HandleInheritability inheritability,
        bool rejectRemoteClients,
        PipeSecurity? pipeSecurity) {

        var openMode = direction switch {
            PipeDirection.In => FILE_FLAGS_AND_ATTRIBUTES.PIPE_ACCESS_INBOUND,
            PipeDirection.Out => FILE_FLAGS_AND_ATTRIBUTES.PIPE_ACCESS_OUTBOUND,
            PipeDirection.InOut => FILE_FLAGS_AND_ATTRIBUTES.PIPE_ACCESS_DUPLEX,
            _ => throw new InvalidEnumArgumentException(nameof(PipeDirection)),
        };
        if ((options & PipeOptions.WriteThrough) != 0) {
            openMode |= FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_WRITE_THROUGH;
            options &= ~PipeOptions.WriteThrough;
        }
        bool isAsync = false;
        if ((options & PipeOptions.Asynchronous) != 0) {
            openMode |= FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OVERLAPPED;
            options &= ~PipeOptions.Asynchronous;
            isAsync = true;
        }
        if ((options & PipeOptions.CurrentUserOnly) != 0) {
            throw new NotSupportedException("PipeOptions.CurrentUserOnly is not supported");
        }
        if ((options & PipeOptions.FirstPipeInstance) != 0) {
            openMode |= FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_FIRST_PIPE_INSTANCE;
            options &= ~PipeOptions.FirstPipeInstance;
        }
        if (options != 0) {
            throw new NotSupportedException($"Unknown PipeOptions value {(int)options}");
        }

        uint maxInstances = maxNumberOfServerInstances == NamedPipeServerStream.MaxAllowedServerInstances ?
            PInvoke.PIPE_UNLIMITED_INSTANCES :
            (uint)maxNumberOfServerInstances;

        NAMED_PIPE_MODE pipeMode = transmissionMode switch {
            PipeTransmissionMode.Byte => NAMED_PIPE_MODE.PIPE_TYPE_BYTE | NAMED_PIPE_MODE.PIPE_READMODE_BYTE,
            PipeTransmissionMode.Message => NAMED_PIPE_MODE.PIPE_TYPE_MESSAGE | NAMED_PIPE_MODE.PIPE_READMODE_MESSAGE,
            _ => throw new InvalidEnumArgumentException(nameof(PipeTransmissionMode)),
        };
        if (rejectRemoteClients) {
            pipeMode |= NAMED_PIPE_MODE.PIPE_REJECT_REMOTE_CLIENTS;
        }

        var sa = new SECURITY_ATTRIBUTES() {
            nLength = (uint)Unsafe.SizeOf<SECURITY_ATTRIBUTES>(),
            lpSecurityDescriptor = null,
            bInheritHandle = inheritability == HandleInheritability.Inheritable,
        };
        var sd = pipeSecurity?.GetSecurityDescriptorBinaryForm();

        HANDLE h;
        string pipeFullPath = @"\\.\pipe\" + pipePath;
        unsafe {
            fixed (byte* psd = sd) {
                fixed (char* pipePathPtr = @"\\.\pipe\" + pipePath) {
                    // this is safe thanks to https://github.com/dotnet/roslyn/issues/6707 which led to
                    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code#247-the-fixed-statement
                    sa.lpSecurityDescriptor = psd;
                    h = PInvoke.CreateNamedPipe(
                        pipePathPtr,
                        openMode,
                        pipeMode,
                        maxInstances,
                        (uint)outBufferSize,
                        (uint)inBufferSize,
                        0,
                        &sa);
                    if (h == HANDLE.INVALID_HANDLE_VALUE) {
                        throw new Win32Exception(nameof(PInvoke.CreateNamedPipe));
                    }
                }
            }
        }
        return new(
            direction,
            isAsync,
            false,
            new SafePipeHandle(h, true));
    }
}
