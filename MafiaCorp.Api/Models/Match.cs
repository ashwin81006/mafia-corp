namespace MafiaCorp.Api.Models
{
    public class Match
    {
        public int Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string WinningFaction { get; set; } = string.Empty;
        public int PlayerCount { get; set; }

        public ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();
    }
}
