using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MafiaCorp.Client.Services;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MafiaCorp.Client.ViewModels;

public partial class GamePlayer : ObservableObject
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private bool isAlive = true;

    [ObservableProperty]
    private bool isLocalPlayer = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string role = string.Empty;

    public string Initial => !string.IsNullOrEmpty(Username) ? Username[..1].ToUpper() : "?";

    public string DisplayText => IsAlive 
        ? Username 
        : (!string.IsNullOrEmpty(Role) ? $"{Username} ({Role})" : Username);
}

public class ChatEntry
{
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsMafia { get; set; }
    public Color TagColor { get; set; } = Colors.Gray;
    public Color MessageColor { get; set; } = Colors.White;
    public string TagText { get; set; } = string.Empty;
}

[QueryProperty(nameof(Role), "Role")]
[QueryProperty(nameof(Username), "Username")]
[QueryProperty(nameof(UserId), "UserId")]
public partial class GameViewModel : ObservableObject, IDisposable
{
    private readonly GameClientService _gameClient;
    private CancellationTokenSource? _timerCts;

    [ObservableProperty]
    private string role = string.Empty;

    partial void OnRoleChanged(string value)
    {
        switch (value.ToUpper())
        {
            case "MAFIA":
                RoleIcon = "🔫";
                RoleColor = Color.FromArgb("#EF4444");
                break;
            case "DOCTOR":
                RoleIcon = "💉";
                RoleColor = Color.FromArgb("#3B82F6");
                break;
            case "DETECTIVE":
                RoleIcon = "🕵️";
                RoleColor = Color.FromArgb("#F59E0B");
                break;
            default:
                RoleIcon = "👤";
                RoleColor = Color.FromArgb("#10B981");
                break;
        }
        UpdatePhaseState(Phase);
    }

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string userId = string.Empty;

    [ObservableProperty]
    private int localPlayerId = -1;

    [ObservableProperty]
    private bool isLocalPlayerAlive = true;

    [ObservableProperty]
    private string phase = "Starting...";

    [ObservableProperty]
    private string phaseIcon = "⏳";

    [ObservableProperty]
    private Color phaseBadgeColor = Color.FromArgb("#71717A");

    [ObservableProperty]
    private string phaseDescription = "Connecting to game...";

    [ObservableProperty]
    private string roleIcon = "👤";

    [ObservableProperty]
    private Color roleColor = Color.FromArgb("#10B981");

    [ObservableProperty]
    private int currentViewTab = 0;

    [ObservableProperty]
    private bool isPlayersTabVisible = true;

    [ObservableProperty]
    private bool isChatTabVisible = false;

    [ObservableProperty]
    private int unreadChatCount = 0;

    [ObservableProperty]
    private string chatTabTitle = "Chat";

    [ObservableProperty]
    private Color playersTabBackground = Color.FromArgb("#E53935");

    [ObservableProperty]
    private Color chatTabBackground = Color.FromArgb("#20202A");

    [ObservableProperty]
    private string chatInput = string.Empty;

    [ObservableProperty]
    private bool isChatEnabled = false;

    [ObservableProperty]
    private string chatPlaceholder = "Chat is disabled during the night...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingDeadRoster))]
    private bool isShowingAliveRoster = true;

    public bool IsShowingDeadRoster => !IsShowingAliveRoster;

    [ObservableProperty]
    private Color aliveTabBg = Color.FromArgb("#065F46");

    [ObservableProperty]
    private Color deadTabBg = Color.FromArgb("#1E1E26");

    [ObservableProperty]
    private Color aliveTabText = Colors.White;

    [ObservableProperty]
    private Color deadTabText = Color.FromArgb("#9CA3AF");

    public string AliveTabTitle => $"ALIVE ({AlivePlayers.Count})";
    public string DeadTabTitle => $"DEAD ({DeadPlayers.Count})";

    [ObservableProperty]
    private string actionButtonText = "Execute Action";

    [ObservableProperty]
    private bool isActionEnabled = false;

    [ObservableProperty]
    private int timeRemaining = 0;

    [ObservableProperty]
    private string timerText = "--";

    [ObservableProperty]
    private GamePlayer? selectedPlayer;

    // Game Over Results Properties
    [ObservableProperty]
    private bool isGameOver = false;

    [ObservableProperty]
    private string gameOverTitle = string.Empty;

    [ObservableProperty]
    private Color gameOverTitleColor = Colors.White;

    [ObservableProperty]
    private string gameOverSubtitle = string.Empty;

    [ObservableProperty]
    private string gameOverRoleText = string.Empty;

    [ObservableProperty]
    private string gameOverStatusText = string.Empty;

    public ObservableCollection<GamePlayer> Players { get; } = new();
    public ObservableCollection<GamePlayer> AlivePlayers { get; } = new();
    public ObservableCollection<GamePlayer> DeadPlayers { get; } = new();
    public ObservableCollection<ChatEntry> ChatLog { get; } = new();

    public GameViewModel(GameClientService gameClient)
    {
        _gameClient = gameClient;
        _gameClient.OnMessageReceived += OnMessageReceived;
        Initialize();
    }

    public void Initialize()
    {
        if (_gameClient.LastRoomStateJson != null)
        {
            ParseRoomStateJson(_gameClient.LastRoomStateJson);
        }

        if (_gameClient.LastPhaseJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(_gameClient.LastPhaseJson);
                var newPhase = doc.RootElement.GetProperty("phase").GetString() ?? "Unknown";
                UpdatePhaseState(newPhase);
                if (doc.RootElement.TryGetProperty("durationSeconds", out var durProp))
                {
                    StartTimer(durProp.GetInt32());
                }
            }
            catch { }
        }
    }

    private void ParseRoomStateJson(string json)
    {
        if (IsGameOver) return; // Do not overwrite results once game is over

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("players", out var playersProp))
            {
                Players.Clear();
                AlivePlayers.Clear();
                DeadPlayers.Clear();
                foreach (var p in playersProp.EnumerateArray())
                {
                    var pName = p.GetProperty("username").GetString() ?? "Unknown";
                    var pRole = p.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
                    var isMe = !string.IsNullOrEmpty(Username) && pName.Equals(Username, StringComparison.OrdinalIgnoreCase);
                    var player = new GamePlayer
                    {
                        Id = p.GetProperty("id").GetInt32(),
                        Username = pName,
                        IsAlive = p.GetProperty("isAlive").GetBoolean(),
                        IsLocalPlayer = isMe,
                        Role = pRole
                    };
                    Players.Add(player);
                    if (player.IsAlive)
                        AlivePlayers.Add(player);
                    else
                        DeadPlayers.Add(player);

                    if (isMe)
                    {
                        LocalPlayerId = player.Id;
                        IsLocalPlayerAlive = player.IsAlive;
                    }
                }

                if (!IsLocalPlayerAlive)
                {
                    IsChatEnabled = false;
                    IsActionEnabled = false;
                    ChatPlaceholder = "☠️ You are dead. Spectating only...";
                    ActionButtonText = "Eliminated (Spectating)";
                }

                UpdateRosterTabStyles();
            }
        }
        catch { }
    }

    private void StartTimer(int seconds)
    {
        try
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();
        }
        catch (ObjectDisposedException) { }

        _timerCts = new CancellationTokenSource();
        var cts = _timerCts;
        var token = cts.Token;
        TimeRemaining = seconds;
        TimerText = $"{TimeRemaining}s";

        Task.Run(async () =>
        {
            try
            {
                while (TimeRemaining > 0 && !token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    if (token.IsCancellationRequested) break;
                    TimeRemaining--;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        TimerText = $"{TimeRemaining}s";
                    });
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void CheckUnreadChat()
    {
        if (!IsChatTabVisible)
        {
            UnreadChatCount++;
            ChatTabTitle = $"Chat ({UnreadChatCount})";
        }
    }

    private void AddSystemMessage(string message, bool isPrivate = false)
    {
        ChatLog.Add(new ChatEntry
        {
            Sender = isPrivate ? "PRIVATE SYSTEM" : "SYSTEM",
            Message = message,
            IsSystem = true,
            IsPrivate = isPrivate,
            TagText = isPrivate ? "[PRIVATE SYSTEM]" : "[SYSTEM]",
            TagColor = isPrivate ? Color.FromArgb("#FFB74D") : Color.FromArgb("#4DD0E1"),
            MessageColor = isPrivate ? Color.FromArgb("#FFE082") : Color.FromArgb("#ECEFF1")
        });
        CheckUnreadChat();
    }

    private void AddMafiaMessage(string sender, string message)
    {
        ChatLog.Add(new ChatEntry
        {
            Sender = sender,
            Message = message,
            IsMafia = true,
            TagText = $"[MAFIA] {sender}:",
            TagColor = Color.FromArgb("#EF5350"),
            MessageColor = Color.FromArgb("#FFCDD2")
        });
        CheckUnreadChat();
    }

    private void AddPlayerMessage(string sender, string message)
    {
        ChatLog.Add(new ChatEntry
        {
            Sender = sender,
            Message = message,
            TagText = $"[{sender}]:",
            TagColor = Color.FromArgb("#81C784"),
            MessageColor = Color.FromArgb("#FFFFFF")
        });
        CheckUnreadChat();
    }

    private void UpdatePhaseState(string newPhase)
    {
        Phase = newPhase;

        if (IsGameOver)
        {
            IsActionEnabled = false;
            ActionButtonText = "Match Finished";
            IsChatEnabled = false;
            PhaseDescription = "Game over! Review final match results.";
            return;
        }

        // Configure living vs eliminated player interaction
        if (IsLocalPlayerAlive)
        {
            // Public chat is only permitted during DAY and VOTING phases for living players
            IsChatEnabled = (newPhase == "DAY" || newPhase == "VOTING");
            ChatPlaceholder = IsChatEnabled ? "Send a message..." : "Chat is disabled during the night...";
        }
        else
        {
            IsChatEnabled = false;
            IsActionEnabled = false;
            ChatPlaceholder = "☠️ You are dead. Spectating only...";
            ActionButtonText = "Eliminated (Spectating)";
        }

        if (newPhase == "NIGHT")
        {
            PhaseIcon = "🌙";
            PhaseBadgeColor = Color.FromArgb("#7E57C2");
            if (IsLocalPlayerAlive)
            {
                switch (Role.ToUpper())
                {
                    case "MAFIA":
                        ActionButtonText = "Kill Target";
                        IsActionEnabled = true;
                        PhaseDescription = "🌙 NIGHT: Select a victim from Alive Citizens & tap 'Kill Target'.";
                        AddSystemMessage("🌙 Night has fallen. Select a victim from Alive Citizens and click 'Kill Target'!", isPrivate: true);
                        break;
                    case "DOCTOR":
                        ActionButtonText = "Protect Target";
                        IsActionEnabled = true;
                        PhaseDescription = "🌙 NIGHT: Select a citizen to protect from attacks tonight.";
                        AddSystemMessage("🌙 Night has fallen. Select a player to protect and click 'Protect Target'!", isPrivate: true);
                        break;
                    case "DETECTIVE":
                        ActionButtonText = "Investigate Target";
                        IsActionEnabled = true;
                        PhaseDescription = "🌙 NIGHT: Select a suspect to reveal their true role.";
                        AddSystemMessage("🌙 Night has fallen. Select a suspect to investigate and click 'Investigate Target'!", isPrivate: true);
                        break;
                    default:
                        ActionButtonText = "Sleep (No Action)";
                        IsActionEnabled = false;
                        PhaseDescription = "🌙 NIGHT: You are an innocent citizen. Sleep peacefully tonight.";
                        AddSystemMessage("🌙 Night has fallen. You are an innocent citizen. Sleep soundly through the night.", isPrivate: true);
                        break;
                }
            }
            else
            {
                PhaseDescription = "🌙 NIGHT: Spectating as night actions unfold.";
            }
            ShowAliveRoster();
            ShowPlayersTab();
        }
        else if (newPhase == "DAY")
        {
            PhaseIcon = "☀️";
            PhaseBadgeColor = Color.FromArgb("#F59E0B");
            if (IsLocalPlayerAlive)
            {
                ActionButtonText = "Discussion Phase";
                IsActionEnabled = false;
                PhaseDescription = "☀️ DAY: Town discussion is open! Strategize and deduce the Mafia.";
            }
            else
            {
                PhaseDescription = "☀️ DAY: Spectating town discussion.";
            }
            AddSystemMessage("☀️ The sun has risen. Discussion is open! Chat with the town to uncover the Mafia.", isPrivate: false);
            ShowChatTab();
        }
        else if (newPhase == "VOTING")
        {
            PhaseIcon = "⚖️";
            PhaseBadgeColor = Color.FromArgb("#EF4444");
            if (IsLocalPlayerAlive)
            {
                ActionButtonText = "Vote to Eliminate";
                IsActionEnabled = true;
                PhaseDescription = "⚖️ VOTING: Tap a suspect from Alive Citizens and tap 'Vote to Eliminate'.";
            }
            else
            {
                PhaseDescription = "⚖️ VOTING: Citizens are voting on a suspect.";
            }
            AddSystemMessage("⚖️ Voting Phase has begun! Select a suspect from Alive Citizens and click 'Vote to Eliminate'.", isPrivate: false);
            ShowAliveRoster();
            ShowPlayersTab();
        }
        else
        {
            PhaseIcon = "⏳";
            PhaseBadgeColor = Color.FromArgb("#71717A");
            if (IsLocalPlayerAlive)
            {
                ActionButtonText = "Waiting...";
                IsActionEnabled = false;
            }
            PhaseDescription = "Waiting for game progress...";
        }
    }

    private void MarkPlayerDead(int playerId, string? fallbackName, string? role = null)
    {
        var p = Players.FirstOrDefault(x => x.Id == playerId);
        if (p != null)
        {
            p.IsAlive = false;
            if (!string.IsNullOrEmpty(role)) p.Role = role;
        }

        var aliveP = AlivePlayers.FirstOrDefault(x => x.Id == playerId);
        if (aliveP != null)
        {
            AlivePlayers.Remove(aliveP);
            aliveP.IsAlive = false;
            if (!string.IsNullOrEmpty(role)) aliveP.Role = role;
            if (!DeadPlayers.Any(x => x.Id == playerId))
            {
                DeadPlayers.Add(aliveP);
            }
        }
        else
        {
            var deadP = DeadPlayers.FirstOrDefault(x => x.Id == playerId);
            if (deadP != null)
            {
                if (!string.IsNullOrEmpty(role)) deadP.Role = role;
            }
            else
            {
                DeadPlayers.Add(new GamePlayer
                {
                    Id = playerId,
                    Username = fallbackName ?? p?.Username ?? $"Player #{playerId}",
                    IsAlive = false,
                    Role = role ?? p?.Role ?? ""
                });
            }
        }

        if (SelectedPlayer?.Id == playerId)
        {
            SelectedPlayer = null;
        }

        // Check if the local player died
        bool isMe = (playerId == LocalPlayerId) || 
                    (!string.IsNullOrEmpty(Username) && Username.Equals(fallbackName, StringComparison.OrdinalIgnoreCase)) ||
                    (p != null && !string.IsNullOrEmpty(Username) && Username.Equals(p.Username, StringComparison.OrdinalIgnoreCase));

        if (isMe)
        {
            IsLocalPlayerAlive = false;
            IsChatEnabled = false;
            IsActionEnabled = false;
            ChatPlaceholder = "☠️ You are dead. Spectating only...";
            ActionButtonText = "Eliminated (Spectating)";
            AddSystemMessage("☠️ You have been eliminated! You can spectate, but cannot chat or perform actions.", isPrivate: true);
        }

        UpdateRosterTabStyles();
    }

    private void OnMessageReceived(string json)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                if (type == "ROOM_STATE")
                {
                    ParseRoomStateJson(json);
                }
                else if (type == "PHASE_CHANGED")
                {
                    var newPhase = doc.RootElement.GetProperty("phase").GetString() ?? "Unknown";
                    UpdatePhaseState(newPhase);

                    if (doc.RootElement.TryGetProperty("durationSeconds", out var durProp))
                    {
                        StartTimer(durProp.GetInt32());
                    }
                }
                else if (type == "CHAT_MESSAGE" || type == "PUBLIC_CHAT_MESSAGE")
                {
                    var senderId = doc.RootElement.GetProperty("senderId").GetInt32();
                    var msg = doc.RootElement.GetProperty("message").GetString() ?? "";
                    var senderName = Players.FirstOrDefault(p => p.Id == senderId)?.Username ?? $"Player #{senderId}";
                    AddPlayerMessage(senderName, msg);
                }
                else if (type == "MAFIA_CHAT_MESSAGE")
                {
                    var senderId = doc.RootElement.GetProperty("senderId").GetInt32();
                    var msg = doc.RootElement.GetProperty("message").GetString() ?? "";
                    var senderName = Players.FirstOrDefault(p => p.Id == senderId)?.Username ?? $"Player #{senderId}";
                    AddMafiaMessage(senderName, msg);
                }
                else if (type == "PLAYER_DIED")
                {
                    var deadId = doc.RootElement.GetProperty("id").GetInt32();
                    var deadName = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                    var deadRole = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() : null;
                    MarkPlayerDead(deadId, deadName, deadRole);
                    var pName = deadName ?? Players.FirstOrDefault(x => x.Id == deadId)?.Username ?? "A player";
                    var roleStr = !string.IsNullOrEmpty(deadRole) ? $" Their role was {deadRole}." : "";
                    AddSystemMessage($"☠️ {pName} was killed last night!{roleStr}", isPrivate: false);
                }
                else if (type == "DOCTOR_SAVED")
                {
                    AddSystemMessage("🛡️ [Doctor Save] The Doctor successfully protected a player last night!", isPrivate: false);
                }
                else if (type == "PLAYER_ELIMINATED")
                {
                    var elimId = doc.RootElement.GetProperty("id").GetInt32();
                    var elimName = doc.RootElement.TryGetProperty("username", out var eu) ? eu.GetString() : null;
                    var elimRole = doc.RootElement.TryGetProperty("role", out var er) ? er.GetString() : null;
                    MarkPlayerDead(elimId, elimName, elimRole);
                    var pName = elimName ?? Players.FirstOrDefault(x => x.Id == elimId)?.Username ?? "A player";
                    var roleStr = !string.IsNullOrEmpty(elimRole) ? $" Their role was {elimRole}." : "";
                    AddSystemMessage($"⚖️ {pName} was voted out and eliminated!{roleStr}", isPrivate: false);
                }
                else if (type == "VOTING_RESULTS")
                {
                    var blocks = new List<string>();

                    if (doc.RootElement.TryGetProperty("candidateVotes", out var cVotesProp))
                    {
                        foreach (var cv in cVotesProp.EnumerateArray())
                        {
                            var candidate = cv.GetProperty("candidate").GetString() ?? "Unknown";
                            var voteCount = cv.GetProperty("votes").GetInt32();
                            if (voteCount > 0)
                            {
                                var voteSuffix = voteCount == 1 ? "1 vote" : $"{voteCount} votes";
                                var sb = new System.Text.StringBuilder();
                                sb.AppendLine($"{candidate} ({voteSuffix})");
                                if (cv.TryGetProperty("voters", out var votersProp))
                                {
                                    foreach (var v in votersProp.EnumerateArray())
                                    {
                                        var vName = v.GetString();
                                        if (!string.IsNullOrEmpty(vName))
                                        {
                                            sb.AppendLine($"• {vName}");
                                        }
                                    }
                                }
                                blocks.Add(sb.ToString().TrimEnd());
                            }
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("votes", out var votesProp))
                    {
                        var tallies = new Dictionary<string, int>();
                        foreach (var v in votesProp.EnumerateArray())
                        {
                            var target = v.GetProperty("target").GetString() ?? "";
                            if (!string.IsNullOrEmpty(target))
                                tallies[target] = tallies.GetValueOrDefault(target, 0) + 1;
                        }

                        foreach (var kvp in tallies.OrderByDescending(x => x.Value))
                        {
                            var voteSuffix = kvp.Value == 1 ? "1 vote" : $"{kvp.Value} votes";
                            blocks.Add($"{kvp.Key} ({voteSuffix})");
                        }
                    }

                    if (blocks.Count > 0)
                    {
                        AddSystemMessage($"🗳️ VOTING RESULTS:\n\n{string.Join("\n\n", blocks)}", isPrivate: false);
                    }
                    else
                    {
                        AddSystemMessage("🗳️ Voting concluded. No votes were cast.", isPrivate: false);
                    }

                    var isTie = doc.RootElement.TryGetProperty("isTie", out var tieProp) && tieProp.GetBoolean();
                    if (isTie)
                    {
                        AddSystemMessage("⚖️ Voting was tied! No one was eliminated.", isPrivate: false);
                    }
                }
                else if (type == "INVESTIGATION_RESULT")
                {
                    var res = doc.RootElement.GetProperty("result").GetString();
                    var targetId = doc.RootElement.GetProperty("targetId").GetInt32();
                    var targetName = Players.FirstOrDefault(x => x.Id == targetId)?.Username ?? $"Player #{targetId}";
                    AddSystemMessage($"🔍 Investigation Result: {targetName} is {res}!", isPrivate: true);
                }
                else if (type == "GAME_OVER")
                {
                    var winner = doc.RootElement.GetProperty("winner").GetString() ?? "";
                    try
                    {
                        _timerCts?.Cancel();
                        _timerCts?.Dispose();
                        _timerCts = null;
                    }
                    catch (ObjectDisposedException) { }
                    TimerText = "0s";
                    IsActionEnabled = false;
                    ActionButtonText = "Match Finished";
                    IsChatEnabled = false;

                    IsGameOver = true;
                    bool isLocalMafia = Role.Equals("MAFIA", StringComparison.OrdinalIgnoreCase);
                    bool mafiaWon = winner.Equals("MAFIA", StringComparison.OrdinalIgnoreCase);
                    bool didIWin = (isLocalMafia && mafiaWon) || (!isLocalMafia && !mafiaWon);

                    GameOverTitle = didIWin ? "🏆 VICTORY!" : "💀 DEFEAT";
                    GameOverTitleColor = didIWin ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E53935");
                    GameOverSubtitle = mafiaWon 
                        ? "The Mafia has taken over the town!" 
                        : "The Citizens have eliminated the Mafia!";
                    GameOverRoleText = $"Your Role: {Role.ToUpper()}";
                    GameOverStatusText = IsLocalPlayerAlive ? "Status: Survived" : "Status: Eliminated";

                    AddSystemMessage($"🏆 GAME OVER! Winners: {winner}. {(didIWin ? "You Won!" : "You Lost!")}", isPrivate: false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameViewModel] Error parsing message: {ex.Message}");
            }
        });
    }

    [RelayCommand]
    private async Task SendChatAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || !IsChatEnabled || !IsLocalPlayerAlive) return;

        await _gameClient.SendMessageAsync(new { type = "CHAT_MESSAGE", message = ChatInput });
        ChatInput = string.Empty;
    }

    [RelayCommand]
    private async Task ActionAsync()
    {
        if (SelectedPlayer == null || !IsActionEnabled || !IsLocalPlayerAlive) return;

        if (Phase == "NIGHT")
        {
            bool isSelf = (SelectedPlayer.Id == LocalPlayerId) || 
                          (!string.IsNullOrEmpty(Username) && SelectedPlayer.Username.Equals(Username, StringComparison.OrdinalIgnoreCase)) ||
                          SelectedPlayer.IsLocalPlayer;

            if (isSelf && !Role.Equals("DOCTOR", StringComparison.OrdinalIgnoreCase))
            {
                AddSystemMessage("⚠️ You cannot target yourself! Only the Doctor can self-protect.", isPrivate: true);
                return;
            }

            string action = Role.ToUpper() switch
            {
                "MAFIA" => "KILL",
                "DOCTOR" => "PROTECT",
                "DETECTIVE" => "INVESTIGATE",
                _ => ""
            };

            if (!string.IsNullOrEmpty(action))
            {
                await _gameClient.SendMessageAsync(new { 
                    type = "NIGHT_ACTION", 
                    targetPlayerId = SelectedPlayer.Id,
                    message = action 
                });
                AddSystemMessage($"🎯 You targeted {SelectedPlayer.Username} for your night action ({action}).", isPrivate: true);
            }
        }
        else if (Phase == "VOTING")
        {
            await _gameClient.SendMessageAsync(new { type = "CAST_VOTE", targetPlayerId = SelectedPlayer.Id });
            AddSystemMessage($"🗳️ You cast your vote for {SelectedPlayer.Username}.", isPrivate: true);
        }
    }

    [RelayCommand]
    private void ShowAliveRoster()
    {
        IsShowingAliveRoster = true;
        UpdateRosterTabStyles();
    }

    [RelayCommand]
    private void ShowDeadRoster()
    {
        IsShowingAliveRoster = false;
        UpdateRosterTabStyles();
    }

    public void UpdateRosterTabStyles()
    {
        if (IsShowingAliveRoster)
        {
            AliveTabBg = Color.FromArgb("#065F46");
            DeadTabBg = Color.FromArgb("#1E1E26");
            AliveTabText = Colors.White;
            DeadTabText = Color.FromArgb("#9CA3AF");
        }
        else
        {
            AliveTabBg = Color.FromArgb("#1E1E26");
            DeadTabBg = Color.FromArgb("#7F1D1D");
            AliveTabText = Color.FromArgb("#9CA3AF");
            DeadTabText = Colors.White;
        }
        OnPropertyChanged(nameof(AliveTabTitle));
        OnPropertyChanged(nameof(DeadTabTitle));
    }

    [RelayCommand]
    private void ShowPlayersTab()
    {
        CurrentViewTab = 0;
        IsPlayersTabVisible = true;
        IsChatTabVisible = false;
        PlayersTabBackground = Color.FromArgb("#E53935");
        ChatTabBackground = Color.FromArgb("#20202A");
    }

    [RelayCommand]
    private void ShowChatTab()
    {
        CurrentViewTab = 1;
        IsPlayersTabVisible = false;
        IsChatTabVisible = true;
        UnreadChatCount = 0;
        ChatTabTitle = "Chat";
        PlayersTabBackground = Color.FromArgb("#20202A");
        ChatTabBackground = Color.FromArgb("#E53935");
    }

    [RelayCommand]
    private void SelectPlayerCard(GamePlayer? player)
    {
        if (player == null || !player.IsAlive) return;
        SelectedPlayer = player;
    }

    [RelayCommand]
    private async Task ReturnToMainMenuAsync()
    {
        _gameClient.Disconnect();
        await Shell.Current.GoToAsync("//HomePage");
    }

    [RelayCommand]
    private void DismissGameOver()
    {
        IsGameOver = false;
    }

    public void Dispose()
    {
        try
        {
            _timerCts?.Cancel();
        }
        catch (ObjectDisposedException) { }
        finally
        {
            try
            {
                _timerCts?.Dispose();
            }
            catch (ObjectDisposedException) { }
            _timerCts = null;
        }

        _gameClient.OnMessageReceived -= OnMessageReceived;
    }
}
