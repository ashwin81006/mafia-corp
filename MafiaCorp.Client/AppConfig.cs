namespace MafiaCorp.Client;

public static class AppConfig
{
    /// <summary>
    /// The public IP or domain of your MafiaCorp server.
    /// When deploying to Oracle Cloud Always Free VPS, change this to your Oracle VPS Public IPv4 address.
    /// </summary>
    public static string ServerHost { get; set; } = "e90t5egf.free.pwrp.cc";
    public static int GameServerPort { get; set; } = 16450;


    /// <summary>
    /// Base URL for the Web API. For Cloudflare Tunnel, set to "https://xxxx.trycloudflare.com".
    /// </summary>
    public static string ApiBaseUrl { get; set; } = "https://veterans-sensors-visibility-cnet.trycloudflare.com";
}
