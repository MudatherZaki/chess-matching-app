using FluentValidation;
using ChessApp.Backend.DTOs;

namespace ChessApp.Backend.Validators;

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
