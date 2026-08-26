// <copyright file="ValidationRuleCodes.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

public static class ValidationRuleCodes
{
    public const string Ctws003 = "CTWS003";
    public const string Ctws004 = "CTWS004";
    public const string Ctws014 = "CTWS014";
    public const string Ctws023 = "CTWS023";
    public const string Ctws034 = "CTWS034";
    public const string Ctws042 = "CTWS042";
    public const string Ctws043 = "CTWS043";
    public const string Ctws044 = "CTWS044";
    public const string Ctws050 = "CTWS050";
    public const string Ctws051 = "CTWS051";
    public const string Ctws052 = "CTWS052";
    public const string Ctws070 = "CTWS070";
    public const string Ctws072 = "CTWS072";
    public const string Ctws074 = "CTWS074";
    public const string Ctws075 = "CTWS075";
    public const string Ctws077 = "CTWS077";
    public const string Ctws079 = "CTWS079";
    public const string Ctws081 = "CTWS081";
    public const string Ctws083 = "CTWS083";
    public const string Ctws084 = "CTWS084";
    public const string Ctws085 = "CTWS085";
    public const string Ctws111 = "CTWS111";
    public const string Ctws120 = "CTWS120";
    public const string Ctws179 = "CTWS179";
    public const string Ctws180 = "CTWS180";
    public const string Ctws182 = "CTWS182";
    public const string Ctws183 = "CTWS183";
    public const string Ctws184 = "CTWS184";
    public const string Ctws189 = "CTWS189";
    public const string Ctws192 = "CTWS192";
    public const string Ctws195 = "CTWS195";
    public const string Ctws196 = "CTWS196";
    public const string Ctws198 = "CTWS198";
    public const string Ctws199 = "CTWS199";
    public const string Ctws200 = "CTWS200";
    public const string Ctws202 = "CTWS202";
    public const string Ctws203 = "CTWS203";
    public const string Ctws204 = "CTWS204";
    public const string Ctws205 = "CTWS205";
    public const string Ctws208 = "CTWS208";
    public const string Ctws209 = "CTWS209";

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ctws003] = "Missing Ear Tag",
        [Ctws004] = "Invalid Ear Tag. Format must be: AANNNNNNNNNNNN",
        [Ctws014] = "Invalid Breed Code",
        [Ctws023] = "Birth Date cannot be in the future",
        [Ctws034] = "Genetic Dam and Animal Ear Tags match",
        [Ctws042] = "Surrogate Dam and Animal Ear Tags match",
        [Ctws043] = "Surrogate and Genetic Dam Ear Tags match",
        [Ctws044] = "Invalid Sire Ear Tag",
        [Ctws050] = "Sire and Animal Ear Tags match",
        [Ctws051] = "Sire and Genetic Dam Ear Tags match",
        [Ctws052] = "Sire and Surrogate Dam Ear Tags match",
        [Ctws070] = "Invalid Postal Location",
        [Ctws072] = "Check Postal Location",
        [Ctws074] = "Check Postal Location",
        [Ctws075] = "Check Postal Location",
        [Ctws077] = "Check Postal Location",
        [Ctws079] = "Invalid Birth Location",
        [Ctws081] = "Check Birth Location",
        [Ctws083] = "Location not allowed movements",
        [Ctws084] = "Check Birth Location",
        [Ctws085] = "Check Birth Location",
        [Ctws111] = "Ear Tag not issued",
        [Ctws120] = "Ear Tag has only been single ordered",
        [Ctws179] = "Birth Dam Ear Tag unavailable",
        [Ctws180] = "Birth Dam Ear Tag not found",
        [Ctws182] = "Birth Dam and Animal Ear Tag may be same",
        [Ctws183] = "Birth Dam and Gen Dam Ear Tag may be same",
        [Ctws184] = "Birth Dam is not registered",
        [Ctws189] = "Birth Dam not registered",
        [Ctws192] = "Ear Tag has already been used",
        [Ctws195] = "Dam's sex is invalid",
        [Ctws196] = "Sire's sex is invalid",
        [Ctws198] = "Dam is dead on birth date",
        [Ctws199] = "Dam was not on location at birth date",
        [Ctws200] = "Dam has already given birth",
        [Ctws202] = "Dam is too old or too young",
        [Ctws203] = "Application is late",
        [Ctws204] = "Duplicate Ear Tag in file",
        [Ctws205] = "Check Genetic Dam Ear Tag",
        [Ctws208] = "Ear Tag not allowed to be a parent",
        [Ctws209] = "Multiple calvings have occurred",
    };

    public static string GetDescription(string errorCode)
    {
        return Descriptions.TryGetValue(errorCode, out var description)
            ? description
            : "Submission validation error";
    }
}
