using RivianMate.Core.Enums;

namespace RivianMate.Api.Helpers;

/// <summary>
/// Centralized charge type display mappings used across Charging page and ChargingCard.
/// </summary>
public static class ChargeTypeHelper
{
    public static string GetIcon(ChargeType? type) => type switch
    {
        ChargeType.DC_Fast => "charge-l3",
        ChargeType.AC_Level2 => "charge-l2",
        ChargeType.AC_Level1 => "charge-l1",
        _ => "charge-l1"
    };

    public static string GetCssClass(ChargeType? type) => type switch
    {
        ChargeType.DC_Fast => "dcfc",
        ChargeType.AC_Level2 => "level2",
        ChargeType.AC_Level1 => "level1",
        _ => "level1"
    };

    public static string GetDisplayName(ChargeType? type) => type switch
    {
        ChargeType.DC_Fast => "DC Fast Charge",
        ChargeType.AC_Level2 => "Level 2",
        ChargeType.AC_Level1 => "Level 1",
        _ => "AC Charging"
    };
}
