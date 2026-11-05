// <copyright file="SerilogStaticLoggerCollection.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Logging;

/// <summary>
/// Runs the tests that host an app with <c>UseSerilog</c> one at a time: it replaces the static <c>Log.Logger</c>, so
/// two such apps running in parallel would write each other's events.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SerilogStaticLoggerCollection
{
    public const string Name = "Serilog static logger";

    private SerilogStaticLoggerCollection()
    {
    }
}
