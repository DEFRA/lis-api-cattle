// <copyright file="ValidationRuleCodes.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

public static class ValidationRuleCodes
{
    public const string CTWS003 = "CTWS003";
    public const string CTWS004 = "CTWS004";
    public const string CTWS014 = "CTWS014";
    public const string CTWS023 = "CTWS023";
    public const string CTWS034 = "CTWS034";
    public const string CTWS042 = "CTWS042";
    public const string CTWS043 = "CTWS043";
    public const string CTWS044 = "CTWS044";
    public const string CTWS050 = "CTWS050";
    public const string CTWS051 = "CTWS051";
    public const string CTWS052 = "CTWS052";
    public const string CTWS070 = "CTWS070";
    public const string CTWS072 = "CTWS072";
    public const string CTWS074 = "CTWS074";
    public const string CTWS075 = "CTWS075";
    public const string CTWS077 = "CTWS077";
    public const string CTWS079 = "CTWS079";
    public const string CTWS081 = "CTWS081";
    public const string CTWS083 = "CTWS083";
    public const string CTWS084 = "CTWS084";
    public const string CTWS085 = "CTWS085";
    public const string CTWS111 = "CTWS111";
    public const string CTWS120 = "CTWS120";
    public const string CTWS179 = "CTWS179";
    public const string CTWS180 = "CTWS180";
    public const string CTWS182 = "CTWS182";
    public const string CTWS183 = "CTWS183";
    public const string CTWS184 = "CTWS184";
    public const string CTWS189 = "CTWS189";
    public const string CTWS192 = "CTWS192";
    public const string CTWS195 = "CTWS195";
    public const string CTWS196 = "CTWS196";
    public const string CTWS198 = "CTWS198";
    public const string CTWS199 = "CTWS199";
    public const string CTWS200 = "CTWS200";
    public const string CTWS202 = "CTWS202";
    public const string CTWS203 = "CTWS203";
    public const string CTWS204 = "CTWS204";
    public const string CTWS205 = "CTWS205";
    public const string CTWS208 = "CTWS208";
    public const string CTWS209 = "CTWS209";

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        [CTWS003] = "Missing Ear Tag",
        [CTWS004] = "Invalid Ear Tag. Format must be: AANNNNNNNNNNNN",
        [CTWS014] = "Invalid Breed Code",
        [CTWS023] = "Birth Date cannot be in the future",
        [CTWS034] = "Genetic Dam and Animal Ear Tags match",
        [CTWS042] = "Surrogate Dam and Animal Ear Tags match",
        [CTWS043] = "Surrogate and Genetic Dam Ear Tags match",
        [CTWS044] = "Invalid Sire Ear Tag",
        [CTWS050] = "Sire and Animal Ear Tags match",
        [CTWS051] = "Sire and Genetic Dam Ear Tags match",
        [CTWS052] = "Sire and Surrogate Dam Ear Tags match",
        [CTWS070] = "Invalid Postal Location",
        [CTWS072] = "Check Postal Location",
        [CTWS074] = "Check Postal Location",
        [CTWS075] = "Check Postal Location",
        [CTWS077] = "Check Postal Location",
        [CTWS079] = "Invalid Birth Location",
        [CTWS081] = "Check Birth Location",
        [CTWS083] = "Location not allowed movements",
        [CTWS084] = "Check Birth Location",
        [CTWS085] = "Check Birth Location",
        [CTWS111] = "Ear Tag not issued",
        [CTWS120] = "Ear Tag has only been single ordered",
        [CTWS179] = "Birth Dam Ear Tag unavailable",
        [CTWS180] = "Birth Dam Ear Tag not found",
        [CTWS182] = "Birth Dam and Animal Ear Tag may be same",
        [CTWS183] = "Birth Dam and Gen Dam Ear Tag may be same",
        [CTWS184] = "Birth Dam is not registered",
        [CTWS189] = "Birth Dam not registered",
        [CTWS192] = "Ear Tag has already been used",
        [CTWS195] = "Dam's sex is invalid",
        [CTWS196] = "Sire's sex is invalid",
        [CTWS198] = "Dam is dead on birth date",
        [CTWS199] = "Dam was not on location at birth date",
        [CTWS200] = "Dam has already given birth",
        [CTWS202] = "Dam is too old or too young",
        [CTWS203] = "Application is late",
        [CTWS204] = "Duplicate Ear Tag in file",
        [CTWS205] = "Check Genetic Dam Ear Tag",
        [CTWS208] = "Ear Tag not allowed to be a parent",
        [CTWS209] = "Multiple calvings have occurred",
    };

    public static string GetDescription(string errorCode)
    {
        return Descriptions.TryGetValue(errorCode, out var description)
            ? description
            : "Submission validation error";
    }
}
