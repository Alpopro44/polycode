using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PolyCode.UI.Models;

public class SessionState
{
    [JsonPropertyName("session_id")] public string SessionId { get; set; } = "";
    [JsonPropertyName("code_buffer")] public string CodeBuffer { get; set; } = "";
    [JsonPropertyName("users")] public List<UserInfo> Users { get; set; } = new();
    [JsonPropertyName("user_count")] public int UserCount { get; set; }
}

public class UserInfo
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "#CDD6F4";
    [JsonPropertyName("cursor")] public CursorPosition? Cursor { get; set; }
}

public class CursorPosition
{
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("column")] public int Column { get; set; }
}
