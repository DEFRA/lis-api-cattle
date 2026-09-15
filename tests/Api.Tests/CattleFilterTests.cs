// <copyright file="CattleFilterTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Models;

public class CattleFilterTests
{
    private static readonly CattleResponse Angus = new()
    {
        EarTag = "UK200000000001",
        Sex = "Female",
        Breed = "Aberdeen Angus",
        BreedCode = "AA",
        BreedName = "Aberdeen Angus",
        Status = "Alive",
    };

    [Fact]
    public void EmptyFilterMatchesEverything()
    {
        var filter = new CattleFilter();

        Assert.True(filter.IsEmpty);
        Assert.True(filter.Matches(Angus));
    }

    [Theory]
    [InlineData("UK200000000001", true)]
    [InlineData("uk 2000 0000 0001", true)]
    [InlineData("0001", true)]
    [InlineData("UK300", false)]
    public void EarTagMatchesIgnoringCaseAndSpaces(string earTag, bool expected)
    {
        Assert.Equal(expected, new CattleFilter(EarTag: earTag).Matches(Angus));
    }

    [Theory]
    [InlineData("AA", true)]
    [InlineData("aa", true)]
    [InlineData("aberdeen angus", true)]
    [InlineData("Hereford", false)]
    public void BreedMatchesCodeOrNameIgnoringCase(string breed, bool expected)
    {
        Assert.Equal(expected, new CattleFilter(Breed: breed).Matches(Angus));
    }

    [Theory]
    [InlineData("female", true)]
    [InlineData("FEMALE", true)]
    [InlineData("male", false)]
    public void SexMatchesIgnoringCase(string sex, bool expected)
    {
        Assert.Equal(expected, new CattleFilter(Sex: sex).Matches(Angus));
    }

    [Fact]
    public void AllSuppliedFiltersMustMatch()
    {
        Assert.True(new CattleFilter("UK2000", "AA", "female").Matches(Angus));
        Assert.False(new CattleFilter("UK2000", "AA", "male").Matches(Angus));
    }
}
