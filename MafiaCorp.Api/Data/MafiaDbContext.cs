using MafiaCorp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaCorp.Api.Data
{
    public class MafiaDbContext : DbContext
    {
        public MafiaDbContext(DbContextOptions<MafiaDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<PlayerStats> PlayerStats { get; set; } = null!;
        public DbSet<Match> Matches { get; set; } = null!;
        public DbSet<MatchPlayer> MatchPlayers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<PlayerStats>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithOne(u => u.PlayerStats)
                      .HasForeignKey<PlayerStats>(e => e.UserId);
            });

            modelBuilder.Entity<MatchPlayer>(entity =>
            {
                entity.HasKey(e => new { e.MatchId, e.UserId });
                
                entity.HasOne(e => e.Match)
                      .WithMany(m => m.MatchPlayers)
                      .HasForeignKey(e => e.MatchId);
                      
                entity.HasOne(e => e.User)
                      .WithMany(u => u.MatchPlayers)
                      .HasForeignKey(e => e.UserId);
            });
        }
    }
}
