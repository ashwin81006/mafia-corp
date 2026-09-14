namespace MafiaCorp.Api.DTOs
{
    public class MatchResultDto
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string WinningFaction { get; set; } = string.Empty;
        public List<MatchPlayerDto> Players { get; set; } = new List<MatchPlayerDto>();
    }

    public class MatchPlayerDto
    {
        public int UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool Won { get; set; }
        public bool Survived { get; set; }
        public int Kills { get; set; }
    }
}
