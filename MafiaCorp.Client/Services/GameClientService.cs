using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MafiaCorp.Client.Services;

public class GameClientService
{
    private string ServerIp => AppConfig.ServerHost;
    private int ServerPort => AppConfig.GameServerPort;

    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
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
            _client = new TcpClient();
            await _client.ConnectAsync(ServerIp, ServerPort);
            
            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            _isConnected = true;

            // Start reading loop in background
            _ = Task.Run(ReceiveMessagesLoop);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return false;
        }
    }

    private async Task ReceiveMessagesLoop()
    {
        try
        {
            while (_isConnected && _reader != null)
            {
                var line = await _reader.ReadLineAsync();
                if (line == null) break; // Disconnected

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

                // Fire event on main thread or just let UI handle dispatching
                OnMessageReceived?.Invoke(line);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receive error: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task SendMessageAsync(object message)
    {
        if (!_isConnected || _writer == null) return;

        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await _writer.WriteLineAsync(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Send error: {ex.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        if (!_isConnected) return;
        _isConnected = false;

        LastRoomStateJson = null;
        LastPhaseJson = null;
        LastRoleAssignedJson = null;

        try { _reader?.Dispose(); } catch {}
        try { _writer?.Dispose(); } catch {}
        try { _client?.Close(); } catch {}

        OnDisconnected?.Invoke();
    }
}
