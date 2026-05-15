using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PolyCode.UI.Models;

namespace PolyCode.UI.Services;

public class PythonBridge : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly string _host;
    private readonly int _port;

    public string UserId { get; private set; } = "";
    public string SessionId { get; private set; } = "";
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public event Action<UserInfo>? OnUserJoined;
    public event Action<string>? OnUserLeft;
    public event Action<ExecutionResult>? OnExecutionResult;
    public event Action<string, int>? OnCodeUpdate;
    public event Action<CursorInfo>? OnCursorUpdate;
    public event Action<SessionState>? OnSessionReady;
    public event Action<string>? OnConnected;
    public event Action<string>? OnDisconnected;
    public event Action<string>? OnError;

    public PythonBridge(string host = "127.0.0.1", int port = 9765)
    {
        _host = host;
        _port = port;
    }

    public async Task ConnectAsync()
    {
        try
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            await _ws.ConnectAsync(new Uri($"ws://{_host}:{_port}"), _cts.Token);
            _listenTask = ListenAsync(_cts.Token);
            OnConnected?.Invoke($"Connected to PolyCode Engine at {_host}:{_port}");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Connection failed: {ex.Message}");
        }
    }

    public async Task CreateSessionAsync()
    {
        await SendAsync(new { type = "create_session", data = new { } });
    }

    public async Task JoinSessionAsync(string sessionId)
    {
        await SendAsync(new { type = "join_session", data = new { session_id = sessionId } });
    }

    public async Task ExecuteCodeAsync(string code)
    {
        await SendAsync(new { type = "execute", data = new { code } });
    }

    public async Task SendCodeUpdateAsync(string code, int clock = 0)
    {
        await SendAsync(new { type = "code_update", data = new { code, lamport_clock = clock } });
    }

    public async Task SendCursorUpdateAsync(int line, int column)
    {
        await SendAsync(new { type = "cursor_update", data = new { line, column } });
    }

    public async Task RequestStateAsync()
    {
        await SendAsync(new { type = "get_state", data = new { } });
    }

    public async Task ResetAsync()
    {
        await SendAsync(new { type = "reset", data = new { } });
    }

    private async Task SendAsync(object obj)
    {
        if (_ws?.State != WebSocketState.Open) return;
        try
        {
            var json = JsonSerializer.Serialize(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts!.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Send error: {ex.Message}");
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        var buffer = new byte[1024 * 64];
        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ProcessMessage(json);
            }
        }
        catch (WebSocketException ex)
        {
            OnError?.Invoke($"WebSocket error: {ex.Message}");
        }
        catch (OperationCanceledException) { }
        finally
        {
            OnDisconnected?.Invoke("Disconnected from engine");
        }
    }

    private void ProcessMessage(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            var data = root.GetProperty("data");

            switch (type)
            {
                case "session_created":
                case "session_joined":
                    UserId = data.GetProperty("user_id").GetString() ?? "";
                    SessionId = data.GetProperty("session_id").GetString() ?? "";
                    var state = JsonSerializer.Deserialize<SessionState>(data.GetProperty("state").GetRawText());
                    if (state != null)
                        OnSessionReady?.Invoke(state);
                    OnConnected?.Invoke($"Session {SessionId} | Your ID: {UserId}");
                    break;

                case "user_joined":
                    var users = JsonSerializer.Deserialize<List<UserInfo>>(data.GetProperty("users").GetRawText());
                    if (users != null)
                        foreach (var u in users) OnUserJoined?.Invoke(u);
                    break;

                case "user_left":
                    var leftId = data.GetProperty("user_id").GetString() ?? "";
                    OnUserLeft?.Invoke(leftId);
                    break;

                case "exec_result":
                    var result = JsonSerializer.Deserialize<ExecutionResult>(data.GetRawText());
                    if (result != null)
                        OnExecutionResult?.Invoke(result);
                    break;

                case "code_update":
                    var code = data.GetProperty("code").GetString() ?? "";
                    var clock = data.GetProperty("lamport_clock").GetInt32();
                    OnCodeUpdate?.Invoke(code, clock);
                    break;

                case "cursor_update":
                    var cursor = JsonSerializer.Deserialize<CursorInfo>(data.GetRawText());
                    if (cursor != null)
                        OnCursorUpdate?.Invoke(cursor);
                    break;

                case "state":
                    var fullState = JsonSerializer.Deserialize<SessionState>(data.GetRawText());
                    if (fullState != null)
                        OnSessionReady?.Invoke(fullState);
                    break;

                case "error":
                    var msg = root.GetProperty("message").GetString() ?? "";
                    OnError?.Invoke(msg);
                    break;
            }
        }
        catch (JsonException ex)
        {
            OnError?.Invoke($"JSON error: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnected", CancellationToken.None);
            }
            catch { }
        }
        _ws?.Dispose();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _ws?.Dispose();
    }
}
