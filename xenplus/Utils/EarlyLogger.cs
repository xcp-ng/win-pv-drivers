using System.Diagnostics;

namespace XenPlus;

/// <summary>
/// For use in early startup only.
/// </summary>
sealed class EarlyLogger {
    const string SourceName = nameof(XenPlus);

    public void LogError(string message, int eventId = 0) {
        EventLog.WriteEntry(SourceName, message, EventLogEntryType.Error, eventId);
    }

    public void LogWarning(string message, int eventId = 0) {
        EventLog.WriteEntry(SourceName, message, EventLogEntryType.Warning, eventId);
    }

    public void LogInformation(string message, int eventId = 0) {
        EventLog.WriteEntry(SourceName, message, EventLogEntryType.Information, eventId);
    }
}
