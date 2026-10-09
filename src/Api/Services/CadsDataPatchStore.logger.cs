// <copyright file="CadsDataPatchStore.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Microsoft.Extensions.Logging;

public partial class CadsDataPatchStore
{
    [LoggerMessage(LogLevel.Warning, "CADS data patch file for animal {EarTag} on holding {Cph} is not a valid animal and was skipped")]
    partial void LogInvalidPatchFile(string earTag, string cph);

    [LoggerMessage(LogLevel.Warning, "CADS data patch file for animal {EarTag} was not found")]
    partial void LogInvalidPatchFile(string earTag);
}
