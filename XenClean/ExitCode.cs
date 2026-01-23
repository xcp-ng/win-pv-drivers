namespace XenClean {
    internal enum ExitCode : int {
        CleaningSucceeded = 0,
        Error = 1,
        UserCanceled = 2,

        ReadyForOnboard = 64,
        AlreadyOnboarded = 65,
        OnboardDenied = 66,
    }
}
