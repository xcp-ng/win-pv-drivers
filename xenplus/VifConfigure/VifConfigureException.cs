namespace XenPlus.VifConfigure;

sealed class VifConfigureException(int errorCode, string command, string? output) : Exception {
    public int ErrorCode => errorCode;
    public string? Output => output;

    public override string Message =>
        $"Command '{command}' failed with exit code {errorCode}: '{output?.Trim() ?? ""}'";
}
