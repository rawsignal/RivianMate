using FluentAssertions;
using RivianMate.Api.Helpers;
using RivianMate.Core.Enums;
using Xunit;

namespace RivianMate.Tests.Helpers;

public class ChargeTypeHelperTests
{
    [Theory]
    [InlineData(ChargeType.DC_Fast, "charge-l3")]
    [InlineData(ChargeType.AC_Level2, "charge-l2")]
    [InlineData(ChargeType.AC_Level1, "charge-l1")]
    [InlineData(null, "charge-l1")]
    public void GetIcon_ReturnsCorrectIcon_ForEachChargeType(ChargeType? type, string expectedIcon)
    {
        ChargeTypeHelper.GetIcon(type).Should().Be(expectedIcon);
    }

    [Theory]
    [InlineData(ChargeType.DC_Fast, "dcfc")]
    [InlineData(ChargeType.AC_Level2, "level2")]
    [InlineData(ChargeType.AC_Level1, "level1")]
    [InlineData(null, "level1")]
    public void GetCssClass_ReturnsCorrectClass_ForEachChargeType(ChargeType? type, string expectedClass)
    {
        ChargeTypeHelper.GetCssClass(type).Should().Be(expectedClass);
    }

    [Theory]
    [InlineData(ChargeType.DC_Fast, "DC Fast Charge")]
    [InlineData(ChargeType.AC_Level2, "Level 2")]
    [InlineData(ChargeType.AC_Level1, "Level 1")]
    [InlineData(null, "AC Charging")]
    public void GetDisplayName_ReturnsCorrectName_ForEachChargeType(ChargeType? type, string expectedName)
    {
        ChargeTypeHelper.GetDisplayName(type).Should().Be(expectedName);
    }
}
