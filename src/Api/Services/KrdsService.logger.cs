// <copyright file="KrdsService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Microsoft.Extensions.Logging;

public partial class KrdsService
{
    [LoggerMessage(LogLevel.Information, "Retrieved holding details for {Cph} from KRDS")]
    partial void LogRetrievedHoldingDetails(string cph);

    [LoggerMessage(LogLevel.Warning, "Holding {Cph} is not known to KRDS")]
    partial void LogHoldingNotKnownToKrds(string cph);
}
