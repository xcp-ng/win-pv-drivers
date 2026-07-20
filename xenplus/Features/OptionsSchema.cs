using System.Text.Json;
using System.Text.Json.Serialization;

namespace XenPlus.Features;

sealed class AllOptions {
    public ClipboardOptions? ClipboardOptions { get; set; }
    public MemoryInfoOptions? MemoryInfoOptions { get; set; }
    public OSInfoOptions? OSInfoOptions { get; set; }
    public PVVersionInfoOptions? PVVersionInfoOptions { get; set; }
    public VifConfigureOptions? VifConfigureOptions { get; set; }
    public TimeResyncOptions? TimeResyncOptions { get; set; }
    public GarbageCollectOptions? GarbageCollectOptions { get; set; }
    public VolumeInfoOptions? VolumeInfoOptions { get; set; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.General)]
[JsonSerializable(typeof(AllOptions))]
partial class AllOptionsContext : JsonSerializerContext {
}
