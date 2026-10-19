using ChessApp.Backend.Data;
using ChessApp.Backend.Models;
using ChessApp.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Xunit;

namespace ChessApp.Backend.Tests;

public class FindNearbyUsersTests
{
    private static readonly GeometryFactory Geometry = new(new PrecisionModel(), 4326);

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User AvailableUser(string username, double offset, bool hasBoard) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{username}@example.com",
        Username = username,
        PasswordHash = "x",
        IsActive = true,
        IsAvailable = true,
        AvailabilityExpiresAt = DateTime.UtcNow.AddHours(1),
        HasBoard = hasBoard,
        Location = Geometry.CreatePoint(new Coordinate(offset, 0))
    };

    [Fact]
    public async Task HasBoardFilter_IsAppliedBeforeLimit()
    {
        using var db = CreateDbContext();
        var me = AvailableUser("me", 0, hasBoard: false);
        db.Users.Add(me);

        // The three closest players have no board; the two with boards are further away.
        db.Users.Add(AvailableUser("near1", 0.001, hasBoard: false));
        db.Users.Add(AvailableUser("near2", 0.002, hasBoard: false));
        db.Users.Add(AvailableUser("near3", 0.003, hasBoard: false));
        db.Users.Add(AvailableUser("board1", 0.004, hasBoard: true));
        db.Users.Add(AvailableUser("board2", 0.005, hasBoard: true));
        await db.SaveChangesAsync();

        var service = new UserService(db, null!, NullLogger<UserService>.Instance);

        var response = await service.FindNearbyUsersAsync(me.Id, hasBoard: true, limit: 2);

        Assert.Equal(new[] { "board1", "board2" }, response.Users.Select(u => u.Username));
    }

    [Fact]
    public async Task NoHasBoardFilter_ReturnsClosestPlayersUpToLimit()
    {
        using var db = CreateDbContext();
        var me = AvailableUser("me", 0, hasBoard: false);
        db.Users.Add(me);
        db.Users.Add(AvailableUser("near1", 0.001, hasBoard: false));
        db.Users.Add(AvailableUser("board1", 0.002, hasBoard: true));
        db.Users.Add(AvailableUser("near2", 0.003, hasBoard: false));
        await db.SaveChangesAsync();

        var service = new UserService(db, null!, NullLogger<UserService>.Instance);

        var response = await service.FindNearbyUsersAsync(me.Id, limit: 2);

        Assert.Equal(new[] { "near1", "board1" }, response.Users.Select(u => u.Username));
    }
}
