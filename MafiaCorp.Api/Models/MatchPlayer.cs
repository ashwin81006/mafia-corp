namespace MafiaCorp.Api.Models
{
    public class MatchPlayer
    {
        public int MatchId { get; set; }
        public Match? Match { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public string Role { get; set; } = string.Empty;
        public bool Won { get; set; }
        public bool Survived { get; set; }
        public int Kills { get; set; }
        public int VotesReceived { get; set; }
    }
}
