using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class StartSessionDtoValidator : AbstractValidator<StartSessionDto>
{
    public StartSessionDtoValidator()
    {
        RuleFor(x => x.ExamId).NotEmpty();
    }
}
