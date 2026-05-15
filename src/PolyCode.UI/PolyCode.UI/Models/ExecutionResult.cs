using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PolyCode.UI.Models;

public class ExecutionResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("output")] public string Output { get; set; } = "";
    [JsonPropertyName("error")] public string Error { get; set; } = "";
    [JsonPropertyName("variables")] public Dictionary<string, string> Variables { get; set; } = new();
}
