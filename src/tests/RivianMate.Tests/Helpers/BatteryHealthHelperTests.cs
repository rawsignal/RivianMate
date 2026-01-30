using FluentAssertions;
using RivianMate.Api.Helpers;
using Xunit;

namespace RivianMate.Tests.Helpers;

public class BatteryHealthHelperTests
{
    [Theory]
    [InlineData(100, "#4ADE80")]
    [InlineData(95, "#4ADE80")]
    [InlineData(94.9, "#7DD87D")]
    [InlineData(90, "#7DD87D")]
    [InlineData(89.9, "#DEB526")]
    [InlineData(85, "#DEB526")]
    [InlineData(84.9, "#F59E0B")]
    [InlineData(80, "#F59E0B")]
    [InlineData(79.9, "#EF4444")]
    [InlineData(50, "#EF4444")]
    public void GetHealthColor_ReturnsCorrectColor_ForEachRange(double percent, string expectedColor)
    {
        BatteryHealthHelper.GetHealthColor(percent).Should().Be(expectedColor);
    }

    [Theory]
    [InlineData(100, "Excellent")]
    [InlineData(95, "Excellent")]
    [InlineData(94.9, "Very Good")]
    [InlineData(90, "Very Good")]
    [InlineData(89.9, "Good")]
    [InlineData(85, "Good")]
    [InlineData(84.9, "Fair")]
    [InlineData(80, "Fair")]
    [InlineData(79.9, "Needs Attention")]
    [InlineData(50, "Needs Attention")]
    public void GetHealthStatus_ReturnsCorrectLabel_ForEachRange(double percent, string expectedStatus)
    {
        BatteryHealthHelper.GetHealthStatus(percent).Should().Be(expectedStatus);
    }
}
