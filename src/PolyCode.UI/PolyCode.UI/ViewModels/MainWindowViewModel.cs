using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolyCode.UI.Models;
using PolyCode.UI.Services;

namespace PolyCode.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PythonBridge _bridge;

    public MainWindowViewModel()
    {
        _bridge = new PythonBridge();
        _bridge.OnConnected += (msg) => StatusMessage = msg;
        _bridge.OnDisconnected += (msg) => StatusMessage = msg;
        _bridge.OnError += (msg) => StatusMessage = $"Error: {msg}";
        _bridge.OnExecutionResult += OnExecResult;
        _bridge.OnCursorUpdate += OnRemoteCursor;
        _bridge.OnCodeUpdate += (code) =>
        {
            if (code != Code)
                Code = code;
        };
    }

    [ObservableProperty]
    private string _code = "# PolyCode - Collaborative Python REPL\nprint('Hello from PolyCode!')\n\n";

    [ObservableProperty]
    private string _output = "";

    [ObservableProperty]
    private string _statusMessage = "Disconnected";

    [ObservableProperty]
    private string _connectionStatus = "●";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _cursorLine;

    [ObservableProperty]
    private int _cursorColumn;

    public ObservableCollection<CursorInfo> RemoteCursors { get; } = new();

    [RelayCommand]
    private async Task Connect()
    {
        StatusMessage = "Connecting...";
        await _bridge.ConnectAsync();
        IsConnected = _bridge.IsConnected;
        ConnectionStatus = IsConnected ? "●" : "○";
    }

    [RelayCommand]
    private async Task Run()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to engine";
            return;
        }

        Output = "> Running...\n";
        StatusMessage = "Executing...";
        await _bridge.ExecuteCodeAsync(Code);
    }

    [RelayCommand]
    private async Task SendCode()
    {
        if (!IsConnected) return;
        await _bridge.SendCodeUpdateAsync(Code);
    }

    [RelayCommand]
    private async Task UpdateCursor()
    {
        if (!IsConnected) return;
        await _bridge.SendCursorUpdateAsync(CursorLine, CursorColumn);
    }

    [RelayCommand]
    private async Task Reset()
    {
        if (!IsConnected) return;
        await _bridge.ResetAsync();
        Output = "";
        StatusMessage = "Session reset";
    }

    private void OnExecResult(ExecutionResult result)
    {
        var sb = new System.Text.StringBuilder();

        if (result.Output.Length > 0)
            sb.AppendLine(result.Output);

        if (result.Error.Length > 0)
            sb.AppendLine($"--- Error ---\n{result.Error}");

        if (result.Success)
            StatusMessage = "Executed successfully";
        else
            StatusMessage = "Execution failed";

        Output = sb.ToString();
    }

    private void OnRemoteCursor(CursorInfo cursor)
    {
        var existing = RemoteCursors.FirstOrDefault(c => c.UserId == cursor.UserId);
        if (existing != null)
        {
            existing.Line = cursor.Line;
            existing.Column = cursor.Column;
        }
        else
        {
            RemoteCursors.Add(cursor);
        }

        StatusMessage = $"{RemoteCursors.Count + 1} user(s) connected";
    }
}
