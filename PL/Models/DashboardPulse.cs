using System.Text.Json.Serialization;

namespace Dashboard.PL.Models;

/**
 * Enterprise Pulse Structure: Optimized V7 (Acronym Properties)
 * Renamed properties directly to acronyms to ensure MessagePack 
 * and JSON consistency without additional attributes.
 */
public class BasePulseMetrics
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? rf { get; set; } // rcvdFto

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? pf { get; set; } // procFto

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? gb { get; set; } // genBill

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ft { get; set; } // fwdTrz

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ar { get; set; } // appRcvd

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? rb { get; set; } // rejBill
}

public class PressurePulseMetrics : BasePulseMetrics
{
    public string sc { get; set; } = ""; // scope

    public int sl { get; set; } // systemLoad
}
