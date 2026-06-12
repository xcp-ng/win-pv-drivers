using System.Text.Json;
using System.Text.Json.Serialization;

namespace XenPlus.Clipboard;

public enum ClipboardMessageType {
    // from server
    SetClipboard = -1,
    // from client
    ReportClipboard = 1,
}

[JsonDerivedType(typeof(SetClipboardMessage), (int)ClipboardMessageType.SetClipboard)]
public abstract class ServerMessage {
}

public class SetClipboardMessage : ServerMessage {
    public string? Text { get; set; }
}

[JsonDerivedType(typeof(ReportClipboardMessage), (int)ClipboardMessageType.ReportClipboard)]
public abstract class ClientMessage {
}

public class ReportClipboardMessage : ClientMessage {
    public string? Text { get; set; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Strict)]
[JsonSerializable(typeof(ServerMessage))]
[JsonSerializable(typeof(ClientMessage))]
partial class ClipboardMessageContext : JsonSerializerContext {
}
