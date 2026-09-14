using MafiaCorp.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MafiaCorp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatsController : ControllerBase
    {
        private readonly MafiaDbContext _context;

        public StatsController(MafiaDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyStats()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Unauthorized();

            var stats = await _context.PlayerStats.FirstOrDefaultAsync(s => s.UserId == userId);
            if (stats == null) return NotFound();

            return Ok(new
            {
                stats.GamesPlayed,
                stats.GamesWon,
                stats.GamesLost,
                stats.Kills,
                stats.Deaths
            });
        }
    }
}
