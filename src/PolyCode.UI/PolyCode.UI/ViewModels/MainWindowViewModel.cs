using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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
        _bridge.OnDisconnected += (msg) => { StatusMessage = msg; IsConnected = false; };
        _bridge.OnError += (msg) => StatusMessage = $"Error: {msg}";
        _bridge.OnExecutionResult += OnExecResult;
        _bridge.OnSessionReady += OnSessionReady;
        _bridge.OnCursorUpdate += OnRemoteCursor;
        _bridge.OnUserJoined += OnUserJoined;
        _bridge.OnUserLeft += OnUserLeft;
        _bridge.OnCodeUpdate += OnRemoteCodeUpdate;

        Code = "# PolyCode - P2P Collaborative Python REPL\nprint('Hello from PolyCode!')\n";
    }

    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _output = "";
    [ObservableProperty] private string _statusMessage = "Disconnected";
    [ObservableProperty] private string _connectionStatus = "○";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _sessionId = "";
    [ObservableProperty] private string _userId = "";
    [ObservableProperty] private int _cursorLine;
    [ObservableProperty] private int _cursorColumn;

    public ObservableCollection<UserInfo> ConnectedUsers { get; } = new();

    private bool _isInternalCodeChange;

    [RelayCommand]
    private async Task Connect()
    {
        StatusMessage = "Connecting to PolyCode Engine...";
        await _bridge.ConnectAsync();
    }

    [RelayCommand]
    private async Task CreateSession()
    {
        if (!_bridge.IsConnected) { StatusMessage = "Connect first"; return; }
        StatusMessage = "Creating session...";
        await _bridge.CreateSessionAsync();
    }

    [RelayCommand]
    private async Task JoinSession()
    {
        if (!_bridge.IsConnected || string.IsNullOrEmpty(SessionId))
        {
            StatusMessage = "Enter a Session ID first";
            return;
        }
        StatusMessage = $"Joining session {SessionId}...";
        await _bridge.JoinSessionAsync(SessionId);
    }

    [RelayCommand]
    private async Task Run()
    {
        if (!_bridge.IsConnected) { StatusMessage = "Not connected"; return; }
        Output = "▸ Running...\n";
        StatusMessage = "Executing...";
        await _bridge.ExecuteCodeAsync(Code);
    }

    public async Task OnCodeEdited(string newCode)
    {
        if (_isInternalCodeChange) return;
        Code = newCode;
        if (_bridge.IsConnected)
            await _bridge.SendCodeUpdateAsync(newCode);
    }

    public async Task OnCursorMoved(int line, int column)
    {
        CursorLine = line;
        CursorColumn = column;
        if (_bridge.IsConnected)
            await _bridge.SendCursorUpdateAsync(line, column);
    }

    [RelayCommand]
    private void CopySessionId()
    {
        if (!string.IsNullOrEmpty(SessionId))
        {
            StatusMessage = "Session ID: " + SessionId;
        }
    }

    [RelayCommand]
    private async Task Reset()
    {
        if (!_bridge.IsConnected) return;
        await _bridge.ResetAsync();
        Output = "";
        _isInternalCodeChange = true;
        Code = "";
        _isInternalCodeChange = false;
        StatusMessage = "Session reset";
    }

    private void OnExecResult(ExecutionResult result)
    {
        var sb = new StringBuilder();
        if (result.Output.Length > 0) sb.AppendLine(result.Output);
        if (result.Error.Length > 0) sb.AppendLine($"\u2014 Error \u2014\n{result.Error}");
        Output = sb.ToString();
        StatusMessage = result.Success ? "Executed successfully" : "Execution failed";
    }

    private void OnSessionReady(SessionState state)
    {
        SessionId = state.SessionId;
        IsConnected = true;
        ConnectionStatus = "●";

        ConnectedUsers.Clear();
        foreach (var user in state.Users)
            ConnectedUsers.Add(user);

        if (!string.IsNullOrEmpty(state.CodeBuffer))
        {
            _isInternalCodeChange = true;
            Code = state.CodeBuffer;
            _isInternalCodeChange = false;
        }

        StatusMessage = $"Session: {state.SessionId} | {state.UserCount} user(s)";
    }

    private void OnRemoteCursor(CursorInfo cursor) { }

    private void OnRemoteCodeUpdate(string code, int clock)
    {
        _isInternalCodeChange = true;
        Code = code;
        _isInternalCodeChange = false;
    }

    private void OnUserJoined(UserInfo user)
    {
        if (ConnectedUsers.All(u => u.UserId != user.UserId))
            ConnectedUsers.Add(user);
        StatusMessage = $"{ConnectedUsers.Count} user(s) connected";
    }

    private void OnUserLeft(string userId)
    {
        var user = ConnectedUsers.FirstOrDefault(u => u.UserId == userId);
        if (user != null) ConnectedUsers.Remove(user);
        StatusMessage = $"{ConnectedUsers.Count} user(s) connected";
    }
}
