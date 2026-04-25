using System.Text.Json.Serialization;
using MessagePack;

namespace Dashboard.BLL.Models;

[MessagePackObject]
public class LoadTestMetrics
{
    [Key("status")]
    public string Status { get; set; } = "Stopped"; // Stopped, Running, Stopping

    [Key("rps")]
    public int Rps { get; set; }

    [Key("avg_latency")]
    public double AvgLatency { get; set; }

    [Key("success")]
    public long SuccessCount { get; set; }

    [Key("errors")]
    public long ErrorCount { get; set; }

    [Key("workers")]
    public int ActiveWorkers { get; set; }

    // Diagnostic Segments
    [Key("db_ms")]
    public double DbTimeMs { get; set; }

    [Key("api_ms")]
    public double ApiTimeMs { get; set; }

    // Bottleneck Analysis
    [Key("bottleneck")]
    public string Bottleneck { get; set; } = "None"; // None, DB, API, SignalR, UI
}

[MessagePackObject]
public class EnginePulse
{
    [Key("sc")]
    public string Scope { get; set; } = "Engine";

    [Key("metrics")]
    public LoadTestMetrics Metrics { get; set; } = new();

    [Key("vitals")]
    public EngineVitals Vitals { get; set; } = new();
}

[MessagePackObject]
public class EngineVitals
{
    [Key("cpu")]
    public int Cpu { get; set; }

    [Key("ram")]
    public int Ram { get; set; }

    [Key("db_conn")]
    public int DbConnections { get; set; }
}
