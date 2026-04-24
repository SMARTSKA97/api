using System.Text.Json.Serialization;

namespace Dashboard.PL.Models;

public class SequencedPulse
{
    [JsonPropertyName("g")]
    public string GroupName { get; set; } = string.Empty;

    [JsonPropertyName("sid")]
    public long sid { get; set; }

    [JsonPropertyName("m")]
    public string m { get; set; } = string.Empty;

    [JsonPropertyName("d")]
    public object d { get; set; } = new { };
}

public class SequencedMessage
{
    public long SequenceId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public object Data { get; set; } = new { };
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
