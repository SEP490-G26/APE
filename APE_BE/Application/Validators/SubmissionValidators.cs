using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class FESubmissionInputDtoValidator : AbstractValidator<FESubmissionInputDto>
{
    public FESubmissionInputDtoValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.UserAnswer).NotNull();
    }
}

public class PESubmissionInputDtoValidator : AbstractValidator<PESubmissionInputDto>
{
    public PESubmissionInputDtoValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        
        RuleFor(x => x.Files).NotEmpty();
    }
}
