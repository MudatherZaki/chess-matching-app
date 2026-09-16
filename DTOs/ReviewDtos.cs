namespace ChessApp.Backend.DTOs;

public class CreateReviewRequest
{
    public Guid MatchId { get; set; }
    public Guid RevieweeId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
}

public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid ReviewerId { get; set; }
    public string ReviewerUsername { get; set; } = null!;
    public string? ReviewerPhotoUrl { get; set; }
    public Guid RevieweeId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewListResponse
{
    public List<ReviewDto> Reviews { get; set; } = new();
    public int Count => Reviews.Count;
    public double? AverageRating { get; set; }
}
