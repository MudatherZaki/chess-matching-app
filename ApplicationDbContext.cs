using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using ChessApp.Backend.Models;

namespace ChessApp.Backend.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Proposal> Proposals { get; set; } = null!;
    public DbSet<Match> Matches { get; set; } = null!;
    public DbSet<UserStats> UserStats { get; set; } = null!;
    public DbSet<Block> Blocks { get; set; } = null!;
    public DbSet<RatingSnapshot> RatingSnapshots { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure geospatial support
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // =====================================================
        // USERS
        // =====================================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.IsAvailable)
                .HasFilter("is_available = true");
            entity.HasIndex(e => e.AvailabilityExpiresAt)
                .HasFilter("is_available = true");

            // Spatial index for location queries
            entity.HasIndex(e => e.Location)
                .HasMethod("GIST");

            // Property configs
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.PhotoUrl).HasMaxLength(500);
            entity.Property(e => e.Bio).HasColumnType("text");

            // PostGIS Point for location
            entity.Property(e => e.Location)
                .HasColumnType("geography (point, 4326)");

            entity.Property(e => e.FideId).HasMaxLength(20);
            entity.Property(e => e.ChessComUsername).HasMaxLength(100);
            entity.Property(e => e.ChessComUrl).HasMaxLength(255);
            entity.Property(e => e.LichessUsername).HasMaxLength(100);
            entity.Property(e => e.LichessUrl).HasMaxLength(255);

            // Device tokens array
            entity.Property(e => e.DeviceTokens)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList());

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // Relationships
            entity.HasMany(e => e.ProposalsSent)
                .WithOne(p => p.Proposer)
                .HasForeignKey(p => p.ProposerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ProposalsReceived)
                .WithOne(p => p.Receiver)
                .HasForeignKey(p => p.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.MatchesAsPlayer1)
                .WithOne(m => m.Player1)
                .HasForeignKey(m => m.Player1Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.MatchesAsPlayer2)
                .WithOne(m => m.Player2)
                .HasForeignKey(m => m.Player2Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Stats)
                .WithOne(s => s.User)
                .HasForeignKey<UserStats>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.BlockedBy)
                .WithOne(b => b.Blocked)
                .HasForeignKey(b => b.BlockedId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Blocking)
                .WithOne(b => b.Blocker)
                .HasForeignKey(b => b.BlockerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================
        // PROPOSALS
        // =====================================================

        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.ReceiverId, e.Status })
                .HasFilter("status = 0"); // 0 = Pending

            entity.HasIndex(e => e.ProposerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExpiresAt);

            // Spatial index for meeting location queries
            entity.HasIndex(e => e.MeetingLocation)
                .HasMethod("GIST");

            entity.Property(e => e.Status)
                .HasConversion(new EnumToStringConverter<ProposalStatus>());

            entity.Property(e => e.Message).HasColumnType("text");

            // PostGIS Point for meeting location
            entity.Property(e => e.MeetingLocation)
                .HasColumnType("geography (point, 4326)");

            entity.Property(e => e.MaxDistanceKm)
                .HasDefaultValue(10);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // Constraint: proposer != receiver
            entity.HasCheckConstraint("ck_different_users", "proposer_id != receiver_id");

            // Optional match relationship
            entity.HasOne(e => e.Match)
                .WithOne(m => m.Proposal)
                .HasForeignKey<Proposal>(p => p.MatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================
        // MATCHES
        // =====================================================

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Player1Id);
            entity.HasIndex(e => e.Player2Id);
            entity.HasIndex(e => e.PlayedAt);

            entity.Property(e => e.Outcome)
                .HasConversion(new EnumToStringConverter<MatchOutcome>());

            entity.Property(e => e.Location)
                .HasColumnType("geography (point, 4326)");

            entity.Property(e => e.TimeControl).HasMaxLength(50);
            entity.Property(e => e.Pgn).HasColumnType("text");
            entity.Property(e => e.Notes).HasColumnType("text");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("NOW()");
        });

        // =====================================================
        // USER STATS
        // =====================================================

        modelBuilder.Entity<UserStats>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);

            entity.Property(e => e.LastStatsUpdate)
                .HasDefaultValueSql("NOW()");
        });

        // =====================================================
        // BLOCKS
        // =====================================================

        modelBuilder.Entity<Block>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.BlockerId);
            entity.HasIndex(e => e.BlockedId);

            // Unique constraint: one block per blocker-blocked pair
            entity.HasIndex(e => new { e.BlockerId, e.BlockedId })
                .IsUnique();

            entity.Property(e => e.Reason).HasColumnType("text");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // Constraint: blocker != blocked
            entity.HasCheckConstraint("ck_different_users", "blocker_id != blocked_id");
        });

        // =====================================================
        // RATING SNAPSHOTS
        // =====================================================

        modelBuilder.Entity<RatingSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.Platform });

            entity.Property(e => e.Platform).HasMaxLength(50).IsRequired();

            entity.Property(e => e.CapturedAt)
                .HasDefaultValueSql("NOW()");
        });

        // =====================================================
        // REFRESH TOKENS
        // =====================================================

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);

            entity.Property(e => e.TokenHash)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(e => e.TokenHash)
                .IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");
        });
    }

    // Override SaveChanges to update UpdatedAt
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is User or Match);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
            {
                ((dynamic)entry.Entity).UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
