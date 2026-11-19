// <copyright file="KrdsLocation.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Location details returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="Address">The address of the location.</param>
/// <param name="CorrespondenceAddress">The address the correspondence for the location is sent to.</param>
public sealed record KrdsLocation(KrdsAddress? Address, KrdsAddress? CorrespondenceAddress);
