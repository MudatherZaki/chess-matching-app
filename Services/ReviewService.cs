using Microsoft.EntityFrameworkCore;
using ChessApp.Backend.Data;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Models;

namespace ChessApp.Backend.Services;

public interface IReviewService
{
    Task<ReviewDto> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request);

    Task<ReviewListResponse> GetReviewsForUserAsync(Guid userId, int limit = 20, int offset = 0);

    Task<ReviewListResponse> GetReviewsByUserAsync(Guid userId, int limit = 20, int offset = 0);
}

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(ApplicationDbContext dbContext, ILogger<ReviewService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReviewDto> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request)
    {
        var match = await _dbContext.Matches.FindAsync(request.MatchId);
        if (match == null)
            throw new KeyNotFoundException($"Match {request.MatchId} not found");

        if (match.Player1Id != reviewerId && match.Player2Id != reviewerId)
            throw new UnauthorizedAccessException("Only participants of this match can leave a review");

        var expectedRevieweeId = match.Player1Id == reviewerId ? match.Player2Id : match.Player1Id;
        if (request.RevieweeId != expectedRevieweeId)
            throw new InvalidOperationException("RevieweeId must be the other participant in the match");

        var alreadyReviewed = await _dbContext.Reviews
            .AnyAsync(r => r.MatchId == request.MatchId && r.ReviewerId == reviewerId);

        if (alreadyReviewed)
            throw new InvalidOperationException("You have already reviewed this match");

        var review = new Review
        {
            Id = Guid.NewGuid(),
            MatchId = request.MatchId,
            ReviewerId = reviewerId,
            RevieweeId = request.RevieweeId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Review created: {ReviewId} for match {MatchId} by {ReviewerId} of {RevieweeId}",
            review.Id, request.MatchId, reviewerId, request.RevieweeId);

        var reviewer = await _dbContext.Users.FindAsync(reviewerId);

        return MapToDto(review, reviewer!);
    }

    public async Task<ReviewListResponse> GetReviewsForUserAsync(Guid userId, int limit = 20, int offset = 0)
    {
        var query = _dbContext.Reviews
            .Include(r => r.Reviewer)
            .Where(r => r.RevieweeId == userId);

        var averageRating = await query.AnyAsync()
            ? await query.AverageAsync(r => (double)r.Rating)
            : (double?)null;

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        return new ReviewListResponse
        {
            Reviews = reviews.Select(r => MapToDto(r, r.Reviewer)).ToList(),
            AverageRating = averageRating.HasValue ? Math.Round(averageRating.Value, 2) : null
        };
    }

    public async Task<ReviewListResponse> GetReviewsByUserAsync(Guid userId, int limit = 20, int offset = 0)
    {
        var reviews = await _dbContext.Reviews
            .Include(r => r.Reviewer)
            .Where(r => r.ReviewerId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        return new ReviewListResponse
        {
            Reviews = reviews.Select(r => MapToDto(r, r.Reviewer)).ToList()
        };
    }

    private static ReviewDto MapToDto(Review review, User reviewer)
    {
        return new ReviewDto
        {
            Id = review.Id,
            MatchId = review.MatchId,
            ReviewerId = review.ReviewerId,
            ReviewerUsername = reviewer.Username,
            ReviewerPhotoUrl = reviewer.PhotoUrl,
            RevieweeId = review.RevieweeId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        };
    }
}
