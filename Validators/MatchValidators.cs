using FluentValidation;
using ChessApp.Backend.DTOs;

namespace ChessApp.Backend.Validators;

public class CreateMatchRequestValidator : AbstractValidator<CreateMatchRequest>
{
    private static readonly string[] ValidOutcomes =
    {
        "not_played", "player1_won", "player2_won", "draw",
        // Also accept the PascalCase enum names directly, since ParseOutcome
        // in MatchService accepts both - the validator shouldn't be stricter
        // than what the service actually does.
        "NotPlayed", "Player1Won", "Player2Won", "Draw",
    };

    public CreateMatchRequestValidator()
    {
        RuleFor(x => x.OpponentId).NotEmpty();

        RuleFor(x => x.PlayedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .WithMessage("PlayedAt cannot be in the future");

        RuleFor(x => x.Outcome)
            .NotEmpty()
            .Must(o => ValidOutcomes.Contains(o, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Outcome must be one of: not_played, player1_won, player2_won, draw");

        RuleFor(x => x.TimeControl).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
