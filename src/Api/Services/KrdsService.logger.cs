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

    [LoggerMessage(LogLevel.Information, "Retrieved user account for subject {Subject} with {CphCount} CPH associations from KRDS")]
    partial void LogRetrievedUserAccount(string subject, int cphCount);

    [LoggerMessage(LogLevel.Warning, "Subject {Subject} is not known to KRDS")]
    partial void LogUserAccountNotKnownToKrds(string subject);
}
