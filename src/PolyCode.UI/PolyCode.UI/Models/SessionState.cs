using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PolyCode.UI.Models;

public class SessionState
{
    [JsonPropertyName("code_buffer")]
    public string CodeBuffer { get; set; } = "";

    [JsonPropertyName("cursors")]
    public Dictionary<string, CursorInfo> Cursors { get; set; } = new();

    [JsonPropertyName("user_count")]
    public int UserCount { get; set; }
}
