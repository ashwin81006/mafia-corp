using MafiaCorp.Api.Data;
using MafiaCorp.Api.DTOs;
using MafiaCorp.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace MafiaCorp.Api.Controllers
{
    [ApiController]
    [Route("api/internal/matches")]
    public class InternalMatchController : ControllerBase
    {
        private readonly MafiaDbContext _context;
        private readonly IConfiguration _configuration;

        public InternalMatchController(MafiaDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> ReportMatchResult([FromBody] MatchResultDto result)
        {
            // Verify Internal API Key
            var apiKey = Request.Headers["X-Internal-Secret"].FirstOrDefault();
            var expectedKey = _configuration["InternalApiSecret"];
            if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
            {
                return Unauthorized("Invalid internal API key.");
            }

            var match = new Match
            {
                StartedAt = result.StartedAt,
                EndedAt = result.EndedAt,
                WinningFaction = result.WinningFaction,
                PlayerCount = result.Players.Count
            };

            foreach (var p in result.Players)
            {
                match.MatchPlayers.Add(new MatchPlayer
                {
                    UserId = p.UserId,
                    Role = p.Role,
                    Won = p.Won,
                    Survived = p.Survived,
                    Kills = p.Kills
                });

                // Update Stats
                var stats = await _context.PlayerStats.FindAsync(p.UserId);
                if (stats != null)
                {
                    stats.GamesPlayed++;
                    if (p.Won) stats.GamesWon++;
                    else stats.GamesLost++;
                    
                    stats.Kills += p.Kills;
                    if (!p.Survived) stats.Deaths++;
                }
            }

            _context.Matches.Add(match);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
