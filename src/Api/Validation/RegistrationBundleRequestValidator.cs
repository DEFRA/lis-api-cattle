// <copyright file="RegistrationBundleRequestValidator.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using Defra.Lis.Api.Cts;
using Defra.Lis.Api.Models.Requests;
using FluentValidation;

/// <summary>
/// Checks the shape of a registration bundle before it is queued: everything the CTSWeb Birth request needs is
/// present and in a form CTS accepts. Business rules (known dams, unused ear tags) are left to CTS.
/// </summary>
public class RegistrationBundleRequestValidator : AbstractValidator<RegistrationBundleRequest>
{
    /// <summary>The most animals one bundle can hold, which keeps the SQS message well under its size limit.</summary>
    public const int MaxAnimalsPerBundle = 1000;

    /// <summary>The longest client reference accepted.</summary>
    public const int MaxClientReferenceLength = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationBundleRequestValidator"/> class.
    /// </summary>
    public RegistrationBundleRequestValidator()
    {
        RuleFor(x => x.ClientReference)
            .NotEmpty()
            .MaximumLength(MaxClientReferenceLength);

        RuleFor(x => x.Holding)
            .NotNull();

        RuleFor(x => x.Holding.Cph)
            .Must(CtsFormat.IsCph)
            .WithMessage("The holding CPH must be in the form nn/nnn/nnnn.")
            .When(x => x.Holding != null);

        RuleFor(x => x.Animals)
            .NotEmpty()
            .WithMessage("A registration bundle must contain at least one animal.")
            .Must(animals => animals.Count <= MaxAnimalsPerBundle)
            .WithMessage($"A registration bundle cannot contain more than {MaxAnimalsPerBundle} animals.");

        RuleForEach(x => x.Animals)
            .NotNull()
            .ChildRules(animal =>
            {
                animal.RuleFor(a => a.EarTag)
                    .Must(BeAnEarTag)
                    .WithMessage($"The ear tag is required and cannot be longer than {CtsFormat.MaxEarTagLength} characters in CTS form.");

                animal.RuleFor(a => a.Sex)
                    .Must(sex => CtsFormat.Sex(sex) != null)
                    .WithMessage("The sex must be male or female (m or f).");

                animal.RuleFor(a => a.Breed)
                    .Must(CtsFormat.IsBreedCode)
                    .WithMessage($"The breed must be a CTS breed code of 1 to {CtsFormat.MaxBreedCodeLength} capital letters.");

                animal.RuleFor(a => a.Dam)
                    .NotNull();

                animal.RuleFor(a => a.Dam.Type)
                    .NotEmpty()
                    .When(a => a.Dam != null);

                animal.RuleFor(a => a.Dam.GeneticDamEarTag)
                    .Must(BeAnEarTag)
                    .WithMessage($"The genetic dam ear tag is required and cannot be longer than {CtsFormat.MaxEarTagLength} characters in CTS form.")
                    .When(a => a.Dam != null);

                animal.RuleFor(a => a.Dam.SurrogateDamEarTag)
                    .Must(BeAnOptionalEarTag)
                    .WithMessage($"The surrogate dam ear tag cannot be longer than {CtsFormat.MaxEarTagLength} characters in CTS form.")
                    .When(a => a.Dam != null);

                animal.RuleFor(a => a.Sire!.EarTag)
                    .Must(BeAnOptionalEarTag)
                    .WithMessage($"The sire ear tag cannot be longer than {CtsFormat.MaxEarTagLength} characters in CTS form.")
                    .When(a => a.Sire != null);
            });
    }

    private static bool BeAnEarTag(string? earTag) =>
        CtsFormat.EarTag(earTag) is { Length: <= CtsFormat.MaxEarTagLength };

    private static bool BeAnOptionalEarTag(string? earTag) =>
        string.IsNullOrWhiteSpace(earTag) || BeAnEarTag(earTag);
}
