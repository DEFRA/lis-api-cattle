// <copyright file="KrdsAddress.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

public sealed record KrdsAddress(
    string? AddressLine1,
    string? AddressLine2,
    string? PostTown,
    string? Locality,
    string? Postcode,
    string? Country);
