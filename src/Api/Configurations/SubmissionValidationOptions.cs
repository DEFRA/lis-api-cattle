// <copyright file="SubmissionValidationOptions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

public class SubmissionValidationOptions
{
    public const string SectionName = "SubmissionValidation";

    /// <summary>
    /// Gets or sets minimum age of dam at calving in months (default: 15 months).
    /// </summary>
    public int MinDamAgeInMonths { get; set; } = 15;

    /// <summary>
    /// Gets or sets maximum age of dam at calving in years (default: 20 years).
    /// </summary>
    public int MaxDamAgeInYears { get; set; } = 20;

    /// <summary>
    /// Gets or sets minimum calving interval in days between calves from the same dam (default: 240 days).
    /// </summary>
    public int MinCalvingIntervalDays { get; set; } = 240;

    /// <summary>
    /// Gets or sets maximum allowed days between birth and registration application (default: 27 days).
    /// </summary>
    public int MaxApplicationLateDays { get; set; } = 27;

    /// <summary>
    /// Gets or sets regex pattern for standard UK ear tag format: AANNNNNNNNNNNN (2 letters + 12 digits).
    /// </summary>
    public string EarTagRegexPattern { get; set; } = @"^[A-Za-z]{2}\d{12}$";

    /// <summary>
    /// Gets or sets regex pattern for standard CPH format: NN/NNN/NNNN.
    /// </summary>
    public string CphRegexPattern { get; set; } = @"^\d{2}/\d{3}/\d{4}$";
}
