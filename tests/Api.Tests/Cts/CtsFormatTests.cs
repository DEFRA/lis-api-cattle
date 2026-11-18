// <copyright file="CtsFormatTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Cts;

using Defra.Lis.Api.Cts;

public class CtsFormatTests
{
    [Theory]
    [InlineData("UK 12 3456 100003", "UK123456100003")]
    [InlineData("uk123456100003", "UK123456100003")]
    [InlineData("  UK 1234 5610 0003 ", "UK123456100003")]
    [InlineData("IE 12 3456 789012", "IE123456789012")]
    public void EarTag_CurrentFormatTag_IsSentWithoutSpaces(string entered, string expected) =>
        Assert.Equal(expected, CtsFormat.EarTag(entered));

    [Theory]
    [InlineData("UK A1213 00003", "UK A1213 00003")]
    [InlineData("UKAA2233 00009", "UKAA2233 00009")]
    [InlineData(" uk h4001  00172 ", "UK H4001 00172")]
    [InlineData("UKAA223300012", "UKAA223300012")]
    [InlineData("UK200203400", "UK200203400")]
    public void EarTag_OtherTags_KeepTheirInnerSpaces(string entered, string expected) =>
        Assert.Equal(expected, CtsFormat.EarTag(entered));

    [Fact]
    public void EarTag_SpacedAndUnspacedOlderTags_StayDifferent() =>
        Assert.NotEqual(CtsFormat.EarTag("UKAA2233 00012"), CtsFormat.EarTag("UKAA223300012"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EarTag_WhenMissing_ReturnsNull(string? entered) =>
        Assert.Null(CtsFormat.EarTag(entered));
}
