using FluentAssertions;
using RivianMate.Api.Helpers;
using RivianMate.Core.Entities;
using Xunit;

namespace RivianMate.Tests.Helpers;

public class ActivityTypeHelperTests
{
    [Theory]
    [InlineData(ActivityType.Drive, "navigation")]
    [InlineData(ActivityType.Charging, "zap")]
    [InlineData(ActivityType.Closure, "door-open")]
    [InlineData(ActivityType.Gear, "settings")]
    [InlineData(ActivityType.Power, "power")]
    [InlineData(ActivityType.Climate, "thermometer")]
    [InlineData(ActivityType.Location, "map-pin")]
    [InlineData(ActivityType.Software, "download")]
    [InlineData(ActivityType.Security, "shield")]
    [InlineData(ActivityType.Unknown, "activity")]
    public void GetIcon_ReturnsCorrectIcon_ForEachActivityType(ActivityType type, string expectedIcon)
    {
        ActivityTypeHelper.GetIcon(type).Should().Be(expectedIcon);
    }

    [Theory]
    [InlineData(ActivityType.Drive, "activity-drive")]
    [InlineData(ActivityType.Charging, "activity-charging")]
    [InlineData(ActivityType.Closure, "activity-closure")]
    [InlineData(ActivityType.Gear, "activity-gear")]
    [InlineData(ActivityType.Power, "activity-power")]
    [InlineData(ActivityType.Climate, "activity-climate")]
    [InlineData(ActivityType.Security, "activity-security")]
    [InlineData(ActivityType.Unknown, "")]
    [InlineData(ActivityType.Location, "")]
    [InlineData(ActivityType.Software, "")]
    public void GetCssClass_ReturnsCorrectClass_ForEachActivityType(ActivityType type, string expectedClass)
    {
        ActivityTypeHelper.GetCssClass(type).Should().Be(expectedClass);
    }
}
