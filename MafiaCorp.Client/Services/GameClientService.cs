using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MafiaCorp.Client.Services;

public class GameClientService
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _isConnected;

    public string? LastRoomStateJson { get; private set; }
    public string? LastPhaseJson { get; private set; }
    public string? LastRoleAssignedJson { get; private set; }

    public event Action<string>? OnMessageReceived;
    public event Action? OnDisconnected;

    public async Task<bool> ConnectAsync()
    {
        try
        {
            Disconnect();

            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            var uri = new Uri(AppConfig.WebSocketUrl);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            await _webSocket.ConnectAsync(uri, linkedCts.Token);
            _isConnected = true;

            // Start reading loop in background
            _ = Task.Run(ReceiveMessagesLoop);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameClientService] Connection failed to {AppConfig.WebSocketUrl}: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    private async Task ReceiveMessagesLoop()
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        try
        {
            while (_isConnected && _webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                ms.SetLength(0);

                do
                {
                    result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        _cts?.Token ?? CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var line = Encoding.UTF8.GetString(ms.ToArray());
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.Contains("\"type\":\"ROOM_STATE\""))
                {
                    LastRoomStateJson = line;
                }
                else if (line.Contains("\"type\":\"PHASE_CHANGED\""))
                {
                    LastPhaseJson = line;
                }
                else if (line.Contains("\"type\":\"ROLE_ASSIGNED\""))
                {
                    LastRoleAssignedJson = line;
                }

                OnMessageReceived?.Invoke(line);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameClientService] Receive error: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task SendMessageAsync(object message)
    {
        if (!_isConnected || _webSocket == null || _webSocket.State != WebSocketState.Open) return;

        await _sendLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes), 
                WebSocketMessageType.Text, 
                true, 
                _cts?.Token ?? CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameClientService] Send error: {ex.Message}");
            Disconnect();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Disconnect()
    {
        if (!_isConnected && _webSocket == null) return;
        _isConnected = false;

        LastRoomStateJson = null;
        LastPhaseJson = null;
        LastRoleAssignedJson = null;

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch { }
        _cts = null;

        try
        {
            if (_webSocket != null)
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None)
                              .GetAwaiter().GetResult();
                }
                _webSocket.Dispose();
            }
        }
        catch { }
        _webSocket = null;

        OnDisconnected?.Invoke();
    }
}
