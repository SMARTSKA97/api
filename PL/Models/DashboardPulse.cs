using System.Text.Json.Serialization;
using MessagePack;

namespace Dashboard.PL.Models;

/**
 * Enterprise Pulse Structure: Optimized V7 (Acronym Properties)
 * Renamed properties directly to acronyms to ensure MessagePack 
 * and JSON consistency without additional attributes.
 */
[MessagePackObject(keyAsPropertyName: true)]
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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ba { get; set; } // billAmount

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? fa { get; set; } // fwdAmount

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? fta { get; set; } // ftoAmount
}

[MessagePackObject(keyAsPropertyName: true)]
public class PressurePulseMetrics : BasePulseMetrics
{
    public string sc { get; set; } = ""; // scope

    public int sl { get; set; } // systemLoad

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? c { get; set; } // cpu

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? m { get; set; } // mem

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? d { get; set; } // db
}
