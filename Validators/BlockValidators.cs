using FluentValidation;
using ChessApp.Backend.DTOs;

namespace ChessApp.Backend.Validators;

public class BlockUserRequestValidator : AbstractValidator<BlockUserRequest>
{
    public BlockUserRequestValidator()
    {
        RuleFor(x => x.BlockedUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
