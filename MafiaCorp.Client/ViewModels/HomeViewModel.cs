using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MafiaCorp.Client.Models;
using MafiaCorp.Client.Services;
using System.Text.Json;
using System.Threading.Tasks;

namespace MafiaCorp.Client.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly GameClientService _gameClient;

    [ObservableProperty]
    private PlayerStats? stats;

    [ObservableProperty]
    private string username = string.Empty;
    
    [ObservableProperty]
    private int userId;

    [ObservableProperty]
    private string roomCodeToJoin = string.Empty;

    public string UserInitial => !string.IsNullOrEmpty(Username) ? Username[..1].ToUpper() : "M";

    public string WinRateText
    {
        get
        {
            if (Stats == null || Stats.GamesPlayed == 0) return "0%";
            var rate = (double)Stats.GamesWon / Stats.GamesPlayed * 100.0;
            return $"{rate:0}%";
        }
    }

    partial void OnUsernameChanged(string value)
    {
        OnPropertyChanged(nameof(UserInitial));
    }

    partial void OnStatsChanged(PlayerStats? value)
    {
        OnPropertyChanged(nameof(WinRateText));
    }

    private bool _isJoiningRoom;

    public HomeViewModel(ApiService apiService, GameClientService gameClient)
    {
        _apiService = apiService;
        _gameClient = gameClient;
    }

    public void OnAppearing()
    {
        _gameClient.OnMessageReceived -= OnMessageReceived;
        _gameClient.OnMessageReceived += OnMessageReceived;
        _isJoiningRoom = false;
    }

    public void OnDisappearing()
    {
        _gameClient.OnMessageReceived -= OnMessageReceived;
        _isJoiningRoom = false;
    }

    public async Task InitializeAsync()
    {
        var me = await _apiService.GetMeAsync();
        if (me != null)
        {
            Username = me.Username;
            UserId = me.Id;
        }

        Stats = await _apiService.GetMyStatsAsync();
    }

    private void OnMessageReceived(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var type = doc.RootElement.GetProperty("type").GetString();

        if (type == "ROOM_CREATED")
        {
            var code = doc.RootElement.GetProperty("roomCode").GetString();
            _isJoiningRoom = false;
            
            // The creator must explicitly join the room they just created
            Task.Run(async () => {
                await _gameClient.SendMessageAsync(new { 
                    type = "JOIN_ROOM", 
                    roomCode = code,
                    username = Username,
                    userId = UserId.ToString()
                });
            });

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync($"{nameof(Views.LobbyPage)}?RoomCode={code}&Username={Username}&UserId={UserId}");
            });
        }
        else if (type == "ROOM_STATE")
        {
            if (_isJoiningRoom)
            {
                _isJoiningRoom = false;
                var code = RoomCodeToJoin.Trim().ToUpper();
                if (doc.RootElement.TryGetProperty("roomCode", out var rcProp) && !string.IsNullOrWhiteSpace(rcProp.GetString()))
                {
                    code = rcProp.GetString()!;
                }
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.GoToAsync($"{nameof(Views.LobbyPage)}?RoomCode={code}&Username={Username}&UserId={UserId}");
                });
            }
        }
    }

    [RelayCommand]
    private async Task CreateRoomAsync()
    {
        _isJoiningRoom = false;
        if (await _gameClient.ConnectAsync())
        {
            await _gameClient.SendMessageAsync(new { type = "CREATE_ROOM" });
        }
    }

    [RelayCommand]
    private async Task JoinRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(RoomCodeToJoin)) return;

        _isJoiningRoom = true;
        if (await _gameClient.ConnectAsync())
        {
            await _gameClient.SendMessageAsync(new { 
                type = "JOIN_ROOM", 
                roomCode = RoomCodeToJoin.Trim().ToUpper(),
                username = Username,
                userId = UserId.ToString()
            });
        }
        else
        {
            _isJoiningRoom = false;
        }
    }

    [RelayCommand]
    private async Task QuitAsync()
    {
        _apiService.Logout();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
