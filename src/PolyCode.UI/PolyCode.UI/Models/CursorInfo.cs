using System.Text.Json.Serialization;

namespace PolyCode.UI.Models;

public class CursorInfo
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("column")] public int Column { get; set; }
}
