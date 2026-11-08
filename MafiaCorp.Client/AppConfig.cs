namespace MafiaCorp.Client;

public static class AppConfig
{
    private static string? _webSocketUrl;
    private static string? _apiBaseUrl;

    /// <summary>
    /// Base URL for the Web API.
    /// Local dev: "http://10.0.2.2:5130" (Android emulator) or "http://localhost:5130" (Windows).
    /// Render production: "https://<your-render-app>.onrender.com"
    /// </summary>
    public static string ApiBaseUrl
    {
        get => _apiBaseUrl ?? "http://localhost:5130";
        set => _apiBaseUrl = value;
    }

    /// <summary>
    /// WebSocket URL for the MafiaCorp Game Server.
    /// Local dev: "ws://10.0.2.2:54555" (Android emulator) or "ws://127.0.0.1:54555" (Windows).
    /// Render production: "wss://<your-render-app>.onrender.com/ws"
    /// </summary>
    public static string WebSocketUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(_webSocketUrl)) return _webSocketUrl;
            return $"ws://{ServerHost}:{GameServerPort}";
        }
        set => _webSocketUrl = value;
    }

    /// <summary>
    /// Fallback host & port if WebSocketUrl is not explicitly set.
    /// </summary>
    public static string ServerHost { get; set; } = "127.0.0.1";
    public static int GameServerPort { get; set; } = 54555;
}
