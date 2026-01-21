using RivianMate.Core.Enums;

namespace RivianMate.Api.Services;

/// <summary>
/// Centralized service for unit conversions and formatting.
/// All input values are expected in their "native" units from the Rivian API:
/// - Temperature: Celsius
/// - Speed: m/s (meters per second)
/// - Distance: Miles
/// - Tire pressure: PSI
/// - Efficiency: mi/kWh
/// </summary>
public class UnitConversionService
{
    // === Conversion Constants ===
    private const double MilesToKilometers = 1.60934;
    private const double MpsToMph = 2.23694;
    private const double MpsToKph = 3.6;
    private const double PsiToBar = 0.0689476;
    private const double PsiToKPa = 6.89476;

    // === Temperature Conversions ===

    /// <summary>
    /// Formats temperature value. Input is Celsius from API.
    /// </summary>
    public string FormatTemperature(double? celsius, TemperatureUnit unit)
    {
        if (celsius == null) return "--";

        if (unit == TemperatureUnit.Fahrenheit)
        {
            var fahrenheit = (celsius.Value * 9.0 / 5.0) + 32;
            return $"{fahrenheit:F0}\u00b0F";
        }

        return $"{celsius.Value:F0}\u00b0C";
    }

    /// <summary>
    /// Converts Celsius to the specified unit and returns just the numeric value
    /// </summary>
    public double? ConvertTemperature(double? celsius, TemperatureUnit unit)
    {
        if (celsius == null) return null;

        if (unit == TemperatureUnit.Fahrenheit)
        {
            return (celsius.Value * 9.0 / 5.0) + 32;
        }

        return celsius.Value;
    }

    /// <summary>
    /// Gets the temperature unit suffix
    /// </summary>
    public string GetTemperatureUnit(TemperatureUnit unit)
    {
        return unit == TemperatureUnit.Fahrenheit ? "\u00b0F" : "\u00b0C";
    }

    // === Speed Conversions ===

    /// <summary>
    /// Formats speed value. Input is m/s from API.
    /// Distance unit determines mph vs km/h.
    /// </summary>
    public string FormatSpeed(double? metersPerSecond, DistanceUnit unit)
    {
        if (metersPerSecond == null) return "--";

        if (unit == DistanceUnit.Miles)
        {
            var mph = metersPerSecond.Value * MpsToMph;
            return $"{mph:F0} mph";
        }

        var kph = metersPerSecond.Value * MpsToKph;
        return $"{kph:F0} km/h";
    }

    /// <summary>
    /// Converts m/s to mph or km/h
    /// </summary>
    public double? ConvertSpeed(double? metersPerSecond, DistanceUnit unit)
    {
        if (metersPerSecond == null) return null;

        if (unit == DistanceUnit.Miles)
        {
            return metersPerSecond.Value * MpsToMph;
        }

        return metersPerSecond.Value * MpsToKph;
    }

    /// <summary>
    /// Gets the speed unit suffix
    /// </summary>
    public string GetSpeedUnit(DistanceUnit unit)
    {
        return unit == DistanceUnit.Miles ? "mph" : "km/h";
    }

    // === Distance Conversions ===

    /// <summary>
    /// Formats distance value. Input is miles from API.
    /// </summary>
    public string FormatDistance(double? miles, DistanceUnit unit, string format = "F0")
    {
        if (miles == null) return "--";

        if (unit == DistanceUnit.Kilometers)
        {
            var km = miles.Value * MilesToKilometers;
            return $"{km.ToString(format)} km";
        }

        return $"{miles.Value.ToString(format)} mi";
    }

    /// <summary>
    /// Converts miles to kilometers if needed
    /// </summary>
    public double? ConvertDistance(double? miles, DistanceUnit unit)
    {
        if (miles == null) return null;

        if (unit == DistanceUnit.Kilometers)
        {
            return miles.Value * MilesToKilometers;
        }

        return miles.Value;
    }

    /// <summary>
    /// Gets the distance unit suffix
    /// </summary>
    public string GetDistanceUnit(DistanceUnit unit)
    {
        return unit == DistanceUnit.Miles ? "mi" : "km";
    }

    // === Range Conversions (semantic alias for distance) ===

    /// <summary>
    /// Formats range value. Input is miles from API.
    /// </summary>
    public string FormatRange(double? miles, DistanceUnit unit)
    {
        return FormatDistance(miles, unit);
    }

    /// <summary>
    /// Converts range value (alias for distance)
    /// </summary>
    public double? ConvertRange(double? miles, DistanceUnit unit)
    {
        return ConvertDistance(miles, unit);
    }

    // === Tire Pressure Conversions ===

    /// <summary>
    /// Formats tire pressure value. Input is PSI from API.
    /// </summary>
    public string FormatPressure(double? psi, TirePressureUnit unit)
    {
        if (psi == null) return "--";

        return unit switch
        {
            TirePressureUnit.Bar => $"{(psi.Value * PsiToBar):F1} bar",
            TirePressureUnit.KPa => $"{(psi.Value * PsiToKPa):F0} kPa",
            _ => $"{psi.Value:F0} PSI"
        };
    }

    /// <summary>
    /// Converts PSI to the specified unit
    /// </summary>
    public double? ConvertPressure(double? psi, TirePressureUnit unit)
    {
        if (psi == null) return null;

        return unit switch
        {
            TirePressureUnit.Bar => psi.Value * PsiToBar,
            TirePressureUnit.KPa => psi.Value * PsiToKPa,
            _ => psi.Value
        };
    }

    /// <summary>
    /// Gets the pressure unit suffix
    /// </summary>
    public string GetPressureUnit(TirePressureUnit unit)
    {
        return unit switch
        {
            TirePressureUnit.Bar => "bar",
            TirePressureUnit.KPa => "kPa",
            _ => "PSI"
        };
    }

    // === Efficiency Conversions ===

    /// <summary>
    /// Formats efficiency value. Input is mi/kWh from API.
    /// Converts to km/kWh for metric users.
    /// </summary>
    public string FormatEfficiency(double? miPerKwh, DistanceUnit unit)
    {
        if (miPerKwh == null) return "--";

        if (unit == DistanceUnit.Kilometers)
        {
            var kmPerKwh = miPerKwh.Value * MilesToKilometers;
            return $"{kmPerKwh:F1} km/kWh";
        }

        return $"{miPerKwh.Value:F1} mi/kWh";
    }

    /// <summary>
    /// Converts efficiency from mi/kWh to km/kWh if needed
    /// </summary>
    public double? ConvertEfficiency(double? miPerKwh, DistanceUnit unit)
    {
        if (miPerKwh == null) return null;

        if (unit == DistanceUnit.Kilometers)
        {
            return miPerKwh.Value * MilesToKilometers;
        }

        return miPerKwh.Value;
    }

    /// <summary>
    /// Gets the efficiency unit suffix
    /// </summary>
    public string GetEfficiencyUnit(DistanceUnit unit)
    {
        return unit == DistanceUnit.Miles ? "mi/kWh" : "km/kWh";
    }

    // === Currency Formatting ===

    /// <summary>
    /// Formats a currency amount with the appropriate symbol
    /// </summary>
    public string FormatCurrency(double? amount, string currencyCode)
    {
        if (amount == null) return "--";

        var symbol = SupportedCurrencies.Symbols.GetValueOrDefault(currencyCode, "$");
        return $"{symbol}{amount:F2}";
    }

    /// <summary>
    /// Formats cost per kWh with currency
    /// </summary>
    public string FormatCostPerKwh(double? ratePerKwh, string currencyCode)
    {
        if (ratePerKwh == null) return "--";

        var symbol = SupportedCurrencies.Symbols.GetValueOrDefault(currencyCode, "$");
        return $"{symbol}{ratePerKwh:F3}/kWh";
    }

    // === Odometer Formatting ===

    /// <summary>
    /// Formats odometer value with commas. Input is miles from API.
    /// </summary>
    public string FormatOdometer(double? miles, DistanceUnit unit)
    {
        if (miles == null) return "--";

        if (unit == DistanceUnit.Kilometers)
        {
            var km = miles.Value * MilesToKilometers;
            return $"{km:N0} km";
        }

        return $"{miles.Value:N0} mi";
    }
}
