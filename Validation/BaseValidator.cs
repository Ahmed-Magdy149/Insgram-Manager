using FluentValidation;

namespace Insgram_Manager.Validation;

/// <summary>
/// Base validator providing common validation rule patterns.
/// Concrete validators will inherit from this class in Phase 2.
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
public abstract class BaseValidator<T> : AbstractValidator<T>
{
    protected void RuleForNotEmpty<TProperty>(System.Linq.Expressions.Expression<Func<T, TProperty>> selector, string propertyName)
        where TProperty : class
    {
        RuleFor(selector)
            .NotEmpty()
            .WithMessage($"{propertyName} is required.");
    }

    protected void RuleForMaxLength(System.Linq.Expressions.Expression<Func<T, string?>> selector, int maxLength, string propertyName)
    {
        RuleFor(selector)
            .MaximumLength(maxLength)
            .WithMessage($"{propertyName} must not exceed {maxLength} characters.");
    }
}
