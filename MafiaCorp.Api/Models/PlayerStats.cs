namespace MafiaCorp.Api.Models
{
    public class PlayerStats
    {
        public int UserId { get; set; }
        public User? User { get; set; }

        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
    }
}
