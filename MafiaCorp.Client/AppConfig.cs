namespace MafiaCorp.Client;

public static class AppConfig
{
    private static string? _webSocketUrl;
    private static string? _apiBaseUrl;

    /// <summary>
    /// Set to true to connect to the live Render cloud deployment.
    /// Set to false to connect to localhost during local debugging.
    /// </summary>
    public static bool UseProductionServer { get; set; } = true;

    public const string ProductionApiUrl = "https://mafia-corp.onrender.com";
    public const string ProductionWsUrl = "wss://mafia-corp.onrender.com/ws";

    public const string LocalApiUrl = "http://10.0.2.2:5130"; // 10.0.2.2 for Android emulator, localhost for Windows
    public const string LocalWsUrl = "ws://10.0.2.2:54555";

    /// <summary>
    /// Base URL for the Web API.
    /// </summary>
    public static string ApiBaseUrl
    {
        get => _apiBaseUrl ?? (UseProductionServer ? ProductionApiUrl : LocalApiUrl);
        set => _apiBaseUrl = value;
    }

    /// <summary>
    /// WebSocket URL for the MafiaCorp Game Server.
    /// </summary>
    public static string WebSocketUrl
    {
        get => _webSocketUrl ?? (UseProductionServer ? ProductionWsUrl : LocalWsUrl);
        set => _webSocketUrl = value;
    }

    public static string ServerHost { get; set; } = "127.0.0.1";
    public static int GameServerPort { get; set; } = 54555;
}
