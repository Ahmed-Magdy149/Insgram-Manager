using FluentValidation;
using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Validation;

/// <summary>
/// Validator for ImportRequest. No validation rules defined yet —
/// this establishes the validator structure for Phase 2.
/// </summary>
public sealed class ImportRequestValidator : AbstractValidator<ImportRequest>
{
    public ImportRequestValidator()
    {
        // Validation rules will be added in Phase 2.
        // Example:
        // RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        // RuleFor(x => x.FileStream).NotNull();
    }
}
