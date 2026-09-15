using FluentValidation;
using ChessApp.Backend.DTOs;

namespace ChessApp.Backend.Validators;

// =====================================================
// AUTH
// =====================================================

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Username)
            .NotEmpty()
            .Length(3, 20)
            .Matches("^[a-zA-Z0-9_]+$")
            .WithMessage("Username may only contain letters, numbers, and underscores");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.FullName)
            .MaximumLength(255)
            .When(x => x.FullName != null);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// =====================================================
// PROFILE
// =====================================================

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(255);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.FideId).MaximumLength(20);
        RuleFor(x => x.FideRating).InclusiveBetween(0, 4000)
            .When(x => x.FideRating.HasValue);
        RuleFor(x => x.ChessComUsername).MaximumLength(100);
        RuleFor(x => x.LichessUsername).MaximumLength(100);
    }
}

// =====================================================
// AVAILABILITY
// =====================================================

public class SetAvailabilityRequestValidator : AbstractValidator<SetAvailabilityRequest>
{
    public SetAvailabilityRequestValidator()
    {
        // Only require a real coordinate when the user is actually going
        // available - "go offline" requests send (0, 0) as a placeholder
        // (see mobile's locationStore.setUnavailable) and shouldn't be
        // rejected for it.
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.IsAvailable);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.IsAvailable);

        RuleFor(x => x.ExpiresInHours)
            .InclusiveBetween(1, 24)
            .WithMessage("Availability must expire between 1 and 24 hours from now");
    }
}

// =====================================================
// PROPOSALS
// =====================================================

public class CreateProposalRequestValidator : AbstractValidator<CreateProposalRequest>
{
    public CreateProposalRequestValidator()
    {
        RuleFor(x => x.ReceiverId).NotEmpty();

        RuleFor(x => x.Message).MaximumLength(500);

        RuleFor(x => x.MeetingLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.MeetingLongitude).InclusiveBetween(-180, 180);
    }
}

public class RejectProposalRequestValidator : AbstractValidator<RejectProposalRequest>
{
    public RejectProposalRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

// =====================================================
// MATCHES
// =====================================================

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

// =====================================================
// BLOCKS
// =====================================================

public class BlockUserRequestValidator : AbstractValidator<BlockUserRequest>
{
    public BlockUserRequestValidator()
    {
        RuleFor(x => x.BlockedUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
