using FluentAssertions;
using RivianMate.Api.Services;
using Xunit;

namespace RivianMate.Tests.Services;

public class GeocodingServiceHelperTests
{
    // === BuildShortAddress ===

    [Fact]
    public void BuildShortAddress_FormatsCorrectly_WithAllParts()
    {
        var result = GeocodingService.BuildShortAddress("123", "Main St", "Springfield", "Illinois");
        result.Should().Be("123 Main St, Springfield, IL");
    }

    [Fact]
    public void BuildShortAddress_FormatsCorrectly_WithoutHouseNumber()
    {
        var result = GeocodingService.BuildShortAddress(null, "Main St", "Springfield", "Illinois");
        result.Should().Be("Main St, Springfield, IL");
    }

    [Fact]
    public void BuildShortAddress_FormatsCorrectly_WithoutRoad()
    {
        var result = GeocodingService.BuildShortAddress(null, null, "Springfield", "Illinois");
        result.Should().Be("Springfield, IL");
    }

    [Fact]
    public void BuildShortAddress_FormatsCorrectly_CityOnly()
    {
        var result = GeocodingService.BuildShortAddress(null, null, "Springfield", null);
        result.Should().Be("Springfield");
    }

    [Fact]
    public void BuildShortAddress_FormatsCorrectly_StateOnly()
    {
        var result = GeocodingService.BuildShortAddress(null, null, null, "California");
        result.Should().Be("CA");
    }

    [Fact]
    public void BuildShortAddress_ReturnsUnknownLocation_WhenAllNull()
    {
        var result = GeocodingService.BuildShortAddress(null, null, null, null);
        result.Should().Be("Unknown location");
    }

    [Fact]
    public void BuildShortAddress_ReturnsUnknownLocation_WhenAllEmpty()
    {
        var result = GeocodingService.BuildShortAddress("", "", "", "");
        result.Should().Be("Unknown location");
    }

    [Fact]
    public void BuildShortAddress_SkipsEmptyHouseNumber()
    {
        var result = GeocodingService.BuildShortAddress("", "Main St", "Denver", "Colorado");
        result.Should().Be("Main St, Denver, CO");
    }

    // === AbbreviateState ===

    [Theory]
    [InlineData("California", "CA")]
    [InlineData("New York", "NY")]
    [InlineData("Texas", "TX")]
    [InlineData("Florida", "FL")]
    [InlineData("Illinois", "IL")]
    [InlineData("Pennsylvania", "PA")]
    [InlineData("Ohio", "OH")]
    [InlineData("District of Columbia", "DC")]
    public void AbbreviateState_ReturnsAbbreviation_ForKnownStates(string state, string expected)
    {
        GeocodingService.AbbreviateState(state).Should().Be(expected);
    }

    [Theory]
    [InlineData("Ontario")]
    [InlineData("Bayern")]
    [InlineData("Unknown State")]
    public void AbbreviateState_ReturnsOriginal_ForUnknownStates(string state)
    {
        GeocodingService.AbbreviateState(state).Should().Be(state);
    }

    [Fact]
    public void AbbreviateState_IsCaseInsensitive()
    {
        GeocodingService.AbbreviateState("california").Should().Be("CA");
        GeocodingService.AbbreviateState("CALIFORNIA").Should().Be("CA");
    }
}
