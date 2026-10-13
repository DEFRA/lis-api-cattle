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

    [LoggerMessage(LogLevel.Information,
        "Retrieved user account for subject {Subject} with {CphCount} CPH associations from KRDS")]
    partial void LogRetrievedUserAccount(string subject, int cphCount);

    [LoggerMessage(LogLevel.Warning, "Subject {Subject} is not known to KRDS")]
    partial void LogUserAccountNotKnownToKrds(string subject);

    [LoggerMessage(LogLevel.Information,
        "Ensured user account for subject {Subject} with {CphCount} CPH associations in KRDS")]
    partial void LogEnsuredUserAccount(string subject, int cphCount);

    [LoggerMessage(LogLevel.Warning,
        "KRDS rejected the user account for subject {Subject}: the email address belongs to another account")]
    partial void LogUserAccountConflict(string subject);

    [LoggerMessage(LogLevel.Warning, "KRDS is temporarily unavailable ({Action})")]
    partial void LogKrdsUnavailable(string action);

    [LoggerMessage(LogLevel.Information, "Retrieved {Count} holdings (page {Page} of {TotalPages}) from KRDS")]
    partial void LogRetrievedHoldings(int count, int page, int totalPages);
}
