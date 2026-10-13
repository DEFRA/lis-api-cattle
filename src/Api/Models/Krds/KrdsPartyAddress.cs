// <copyright file="KrdsPartyAddress.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Party address details returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="AddressLine1">The first line of the address.</param>
/// <param name="AddressLine2">The second line of the address.</param>
/// <param name="AddressTown">The town of the address.</param>
/// <param name="AddressLocality">The locality of the address.</param>
/// <param name="AddressNation">The nation of the address.</param>
/// <param name="AddressPostcode">The postcode of the address.</param>
/// <param name="AddressCountryCode">The country code of the address.</param>
public sealed record KrdsPartyAddress(
    string? AddressLine1,
    string? AddressLine2,
    string? AddressTown,
    string? AddressLocality,
    string? AddressNation,
    string? AddressPostcode,
    string? AddressCountryCode);
