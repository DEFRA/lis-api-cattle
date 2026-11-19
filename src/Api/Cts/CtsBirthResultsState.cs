// <copyright file="CtsBirthResultsState.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Cts;

/// <summary>The state of a <c>Get_Register_Births_Validation_Results</c> call.</summary>
public enum CtsBirthResultsState
{
    /// <summary>CTS returned results for some or all of the rows.</summary>
    Ready,

    /// <summary>CTS has not finished validating the bundle yet (<c>CTWS806</c>).</summary>
    NotReady,

    /// <summary>The call failed. See <see cref="CtsBirthResults.ErrorCode"/>.</summary>
    Failed,
}
