// <copyright file="KrdsUserAccount.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// The user account returned by the keeper-data-api V2 user-accounts endpoint (<c>UserAccountDto</c>).
/// Only the fields this API needs are modelled; the account id and refresh timestamps are not read.
/// </summary>
public sealed record KrdsUserAccount(
    string? Subject,
    string? Email,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    IReadOnlyList<KrdsCphAssociation>? CphAssociations);
