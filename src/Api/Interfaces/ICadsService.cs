// <copyright file="ICadsService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;

public interface ICadsService
{
    Task<IEnumerable<CattleResponse>> GetCattleByCphAsync(string cph);
}
