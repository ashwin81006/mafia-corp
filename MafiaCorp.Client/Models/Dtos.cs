namespace MafiaCorp.Client.Models;

public class LoginResponse
{
    public string Token { get; set; }
}

public class PlayerStats
{
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; }
}
