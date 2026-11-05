// <copyright file="CadsService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Microsoft.Extensions.Logging;

public partial class CadsService
{
    [LoggerMessage(LogLevel.Information, "Retrieved {Count} live animals over {Pages} page(s) for holding {Cph} from CADS")]
    partial void LogRetrievedLiveAnimalsForHolding(int count, int pages, string cph);

    [LoggerMessage(LogLevel.Warning, "Holding {Cph} is not known to CADS")]
    partial void LogHoldingNotKnownToCads(string cph);

    [LoggerMessage(LogLevel.Information, "Retrieved details for animal {EarTag} from CADS")]
    partial void LogRetrievedAnimalDetails(string earTag);

    [LoggerMessage(LogLevel.Warning, "Animal {EarTag} is not known to CADS")]
    partial void LogAnimalNotKnownToCads(string earTag);

    [LoggerMessage(LogLevel.Information, "Added {Count} live animals for holding {Cph} from the CADS data patch")]
    partial void LogAddedAnimalsFromCadsDataPatch(int count, string cph);

    [LoggerMessage(LogLevel.Information, "CADS data patch for holding {Cph} could not be read; returning CADS animals only")]
    partial void LogCadsDataPatchUnavailable(Exception exception, string cph);

    [LoggerMessage(LogLevel.Warning, "CADS data patch could not be read for animal {EarTag}, which CADS does not know; answering 503")]
    partial void LogCadsDataPatchUnreadableForAnimal(Exception exception, string earTag);

    [LoggerMessage(LogLevel.Warning, "CADS is unavailable ({Action}): no response in time, the circuit is open, or it answered 503/504")]
    partial void LogCadsUnavailable(string action);
}
