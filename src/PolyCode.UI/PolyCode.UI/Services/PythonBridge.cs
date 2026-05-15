using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PolyCode.UI.Models;

namespace PolyCode.UI.Services;

public class PythonBridge : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public event Action<CursorInfo>? OnCursorUpdate;
    public event Action<ExecutionResult>? OnExecutionResult;
    public event Action<string>? OnCodeUpdate;
    public event Action<string>? OnConnected;
    public event Action<string>? OnDisconnected;
    public event Action<string>? OnError;

    public string UserId { get; private set; } = "";
    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync(string host = "127.0.0.1", int port = 9765)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { NewLine = "\n", AutoFlush = true };

            _cts = new CancellationTokenSource();
            _listenTask = ListenAsync(_cts.Token);

            OnConnected?.Invoke($"Connected to PolyCode Engine at {host}:{port}");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Connection failed: {ex.Message}");
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line == null) break;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();

                    switch (type)
                    {
                        case "welcome":
                            UserId = root.GetProperty("user_id").GetString() ?? "";
                            OnConnected?.Invoke($"Welcome! Your ID: {UserId}");
                            break;

                        case "exec_result":
                            var result = JsonSerializer.Deserialize<ExecutionResult>(
                                root.GetProperty("data").GetRawText(),
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                            );
                            if (result != null)
                                OnExecutionResult?.Invoke(result);
                            break;

                        case "cursor_update":
                            var cursor = JsonSerializer.Deserialize<CursorInfo>(
                                root.GetProperty("data").GetRawText(),
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                            );
                            if (cursor != null)
                                OnCursorUpdate?.Invoke(cursor);
                            break;

                        case "code_update":
                            var code = root.GetProperty("data").GetProperty("code").GetString() ?? "";
                            OnCodeUpdate?.Invoke(code);
                            break;

                        case "error":
                            var msg = root.GetProperty("message").GetString() ?? "";
                            OnError?.Invoke(msg);
                            break;
                    }
                }
                catch (JsonException ex)
                {
                    OnError?.Invoke($"JSON parse error: {ex.Message}");
                }
            }
        }
        catch (IOException) { }
        catch (OperationCanceledException) { }
        finally
        {
            OnDisconnected?.Invoke("Disconnected from engine");
        }
    }

    public async Task ExecuteCodeAsync(string code)
    {
        await SendAsync(new { type = "execute", data = new { code } });
    }

    public async Task SendCodeUpdateAsync(string code)
    {
        await SendAsync(new { type = "code_update", data = new { code } });
    }

    public async Task SendCursorUpdateAsync(int line, int column)
    {
        await SendAsync(new { type = "cursor_update", data = new { cursor = new { line, column } } });
    }

    public async Task RequestStateAsync()
    {
        await SendAsync(new { type = "get_state" });
    }

    public async Task ResetAsync()
    {
        await SendAsync(new { type = "reset" });
    }

    private async Task SendAsync(object obj)
    {
        if (_writer == null) return;
        try
        {
            var json = JsonSerializer.Serialize(obj);
            await _writer.WriteLineAsync(json);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Send error: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _writer?.Close();
        _reader?.Close();
        _stream?.Close();
        _client?.Close();
    }

    public void Dispose()
    {
        Disconnect();
        _cts?.Dispose();
    }
}
