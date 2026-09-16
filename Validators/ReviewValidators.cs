using FluentValidation;
using ChessApp.Backend.DTOs;

namespace ChessApp.Backend.Validators;

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.RevieweeId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
