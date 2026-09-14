using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MafiaCorp.Client.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MafiaCorp.Client.ViewModels;

public class LobbyPlayerItem
{
    public string Username { get; set; } = string.Empty;
    public string Initial => !string.IsNullOrEmpty(Username) ? Username[..1].ToUpper() : "?";
    public bool IsHost { get; set; }
    public bool IsLocalUser { get; set; }
}

[QueryProperty(nameof(RoomCode), "RoomCode")]
[QueryProperty(nameof(Username), "Username")]
[QueryProperty(nameof(UserId), "UserId")]
public partial class LobbyViewModel : ObservableObject, IDisposable
{
    private readonly GameClientService _gameClient;

    [ObservableProperty]
    private string roomCode = string.Empty;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string userId = string.Empty;

    [ObservableProperty]
    private bool isHost;

    [ObservableProperty]
    private string memberCountText = "0 / 6 Players";

    [ObservableProperty]
    private string startStatusText = "Waiting for operatives to assemble (6 minimum)...";

    [ObservableProperty]
    private bool canStart = false;

    [ObservableProperty]
    private string copyStatus = "Tap room code to copy";

    public ObservableCollection<string> Players { get; } = new();
    public ObservableCollection<LobbyPlayerItem> LobbyPlayers { get; } = new();

    public LobbyViewModel(GameClientService gameClient)
    {
        _gameClient = gameClient;
        _gameClient.OnMessageReceived += OnMessageReceived;
    }

    public void Initialize()
    {
        if (_gameClient.LastRoomStateJson != null)
        {
            OnMessageReceived(_gameClient.LastRoomStateJson);
        }
    }

    private void UpdateStatus()
    {
        MemberCountText = $"{LobbyPlayers.Count} / 6 Players";
        if (LobbyPlayers.Count < 6)
        {
            StartStatusText = $"Need {6 - LobbyPlayers.Count} more player(s) to start (6 min)";
            CanStart = false;
        }
        else
        {
            StartStatusText = IsHost ? "Squad complete! Ready to launch." : "Squad complete! Waiting for Host to start.";
            CanStart = IsHost;
        }
    }

    private void OnMessageReceived(string json)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString();

            if (type == "ROOM_STATE")
            {
                if (doc.RootElement.TryGetProperty("roomCode", out var rcProp) && !string.IsNullOrWhiteSpace(rcProp.GetString()))
                {
                    RoomCode = rcProp.GetString()!;
                }

                Players.Clear();
                LobbyPlayers.Clear();
                var playersArray = doc.RootElement.GetProperty("players").EnumerateArray();
                int hostId = -1;
                if (doc.RootElement.TryGetProperty("hostId", out var hostElement))
                {
                    hostId = hostElement.GetInt32();
                }

                foreach (var p in playersArray)
                {
                    var pName = p.GetProperty("username").GetString() ?? "Unknown";
                    var pId = p.GetProperty("id").GetInt32();
                    var isThisHost = (pId == hostId);
                    var isMe = !string.IsNullOrEmpty(Username) && pName.Equals(Username, StringComparison.OrdinalIgnoreCase);

                    Players.Add(pName);
                    LobbyPlayers.Add(new LobbyPlayerItem
                    {
                        Username = pName,
                        IsHost = isThisHost,
                        IsLocalUser = isMe
                    });

                    if (isMe)
                    {
                        IsHost = isThisHost;
                    }
                }

                UpdateStatus();
            }
            else if (type == "PLAYER_JOINED")
            {
                var pObj = doc.RootElement.GetProperty("player");
                var pName = pObj.GetProperty("username").GetString() ?? "Unknown";
                if (!Players.Contains(pName))
                {
                    Players.Add(pName);
                    LobbyPlayers.Add(new LobbyPlayerItem
                    {
                        Username = pName,
                        IsHost = false,
                        IsLocalUser = !string.IsNullOrEmpty(Username) && pName.Equals(Username, StringComparison.OrdinalIgnoreCase)
                    });
                }
                UpdateStatus();
            }
            else if (type == "PLAYER_LEFT")
            {
                // Ignore for now
            }
            else if (type == "ROLE_ASSIGNED")
            {
                var role = doc.RootElement.GetProperty("role").GetString();
                await Shell.Current.GoToAsync($"{nameof(Views.GamePage)}?Role={role}&Username={Username}&UserId={UserId}");
            }
        });
    }

    [RelayCommand]
    private async Task CopyCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(RoomCode)) return;
        await Clipboard.Default.SetTextAsync(RoomCode);
        CopyStatus = "✓ Room Code copied to clipboard!";
        await Task.Delay(2000);
        CopyStatus = "Tap room code to copy";
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        await _gameClient.SendMessageAsync(new { type = "START_GAME" });
    }
    
    [RelayCommand]
    private async Task LeaveLobbyAsync()
    {
        _gameClient.Disconnect();
        await Shell.Current.GoToAsync("..");
    }

    public void Dispose()
    {
        _gameClient.OnMessageReceived -= OnMessageReceived;
    }
}
