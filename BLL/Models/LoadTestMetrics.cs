using System.Text.Json.Serialization;
using MessagePack;

namespace Dashboard.BLL.Models;

[MessagePackObject(keyAsPropertyName: true)]
public class LoadTestMetrics
{
    public string status { get; set; } = "Stopped"; // Stopped, Running, Stopping

    public int rps { get; set; }

    public double avgLatency { get; set; }

    public long successCount { get; set; }

    public long errorCount { get; set; }

    public int activeWorkers { get; set; }

    // Diagnostic Segments
    public double dbTimeMs { get; set; }

    public double apiTimeMs { get; set; }

    // Bottleneck Analysis
    public string bottleneck { get; set; } = "None"; // None, DB, API, SignalR, UI
}

[MessagePackObject(keyAsPropertyName: true)]
public class EnginePulse
{
    public string sc { get; set; } = "Engine";

    public LoadTestMetrics metrics { get; set; } = new();

    public EngineVitals vitals { get; set; } = new();
}

[MessagePackObject(keyAsPropertyName: true)]
public class EngineVitals
{
    public int cpu { get; set; }

    public int ram { get; set; }

    public int dbConn { get; set; }
}
