using FluentValidation;
using ChessApp.Backend.DTOs;

namespace ChessApp.Backend.Validators;

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
