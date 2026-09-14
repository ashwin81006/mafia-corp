namespace MafiaCorp.Api.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        
        public PlayerStats? PlayerStats { get; set; }
        public ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();
    }
}
