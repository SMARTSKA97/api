using System.Text.Json.Serialization;
using MessagePack;

namespace Dashboard.PL.Models;

[MessagePackObject]
public class SequencedPulse
{
    [Key("g")]
    [JsonPropertyName("g")]
    public string GroupName { get; set; } = string.Empty;

    [Key("sid")]
    [JsonPropertyName("sid")]
    public long sid { get; set; }

    [Key("m")]
    [JsonPropertyName("m")]
    public string m { get; set; } = string.Empty;

    [Key("d")]
    [JsonPropertyName("d")]
    public object d { get; set; } = new { };

    [Key("ts")]
    [JsonPropertyName("ts")]
    public long ts { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public class SequencedMessage
{
    public long SequenceId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public object Data { get; set; } = new { };
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
