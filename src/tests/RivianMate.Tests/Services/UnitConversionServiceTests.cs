using FluentAssertions;
using RivianMate.Api.Services;
using RivianMate.Core.Enums;
using Xunit;

namespace RivianMate.Tests.Services;

public class UnitConversionServiceTests
{
    private readonly UnitConversionService _sut = new();

    // === Temperature ===

    [Fact]
    public void FormatTemperature_Fahrenheit_ConvertsCorrectly()
    {
        // 0°C = 32°F
        _sut.FormatTemperature(0, TemperatureUnit.Fahrenheit).Should().Be("32\u00b0F");
    }

    [Fact]
    public void FormatTemperature_Fahrenheit_100C()
    {
        // 100°C = 212°F
        _sut.FormatTemperature(100, TemperatureUnit.Fahrenheit).Should().Be("212\u00b0F");
    }

    [Fact]
    public void FormatTemperature_Celsius_ReturnsAsIs()
    {
        _sut.FormatTemperature(25, TemperatureUnit.Celsius).Should().Be("25\u00b0C");
    }

    [Fact]
    public void FormatTemperature_Null_ReturnsDash()
    {
        _sut.FormatTemperature(null, TemperatureUnit.Fahrenheit).Should().Be("--");
    }

    [Fact]
    public void ConvertTemperature_RoundTrip_0C_Is_32F()
    {
        _sut.ConvertTemperature(0, TemperatureUnit.Fahrenheit).Should().Be(32);
    }

    [Fact]
    public void ConvertTemperature_RoundTrip_100C_Is_212F()
    {
        _sut.ConvertTemperature(100, TemperatureUnit.Fahrenheit).Should().Be(212);
    }

    // === Speed ===

    [Fact]
    public void FormatSpeed_Mph_ConvertsFromMps()
    {
        // 10 m/s ≈ 22 mph
        var result = _sut.FormatSpeed(10, DistanceUnit.Miles);
        result.Should().Be("22 mph");
    }

    [Fact]
    public void FormatSpeed_Kph_ConvertsFromMps()
    {
        // 10 m/s = 36 km/h
        var result = _sut.FormatSpeed(10, DistanceUnit.Kilometers);
        result.Should().Be("36 km/h");
    }

    [Fact]
    public void FormatSpeed_Null_ReturnsDash()
    {
        _sut.FormatSpeed(null, DistanceUnit.Miles).Should().Be("--");
    }

    // === Distance ===

    [Fact]
    public void FormatDistance_Miles_ReturnsAsIs()
    {
        _sut.FormatDistance(100.0, DistanceUnit.Miles).Should().Be("100 mi");
    }

    [Fact]
    public void FormatDistance_Kilometers_ConvertsFromMiles()
    {
        // 100 miles ≈ 161 km
        var result = _sut.FormatDistance(100.0, DistanceUnit.Kilometers);
        result.Should().Be("161 km");
    }

    [Fact]
    public void FormatDistance_Null_ReturnsDash()
    {
        _sut.FormatDistance(null, DistanceUnit.Miles).Should().Be("--");
    }

    // === Tire Pressure ===

    [Fact]
    public void FormatPressure_Psi_ReturnsAsIs()
    {
        _sut.FormatPressure(35.0, TirePressureUnit.Psi).Should().Be("35 PSI");
    }

    [Fact]
    public void FormatPressure_Bar_ConvertsFromPsi()
    {
        // 35 PSI ≈ 2.4 bar
        var result = _sut.FormatPressure(35.0, TirePressureUnit.Bar);
        result.Should().Be("2.4 bar");
    }

    [Fact]
    public void FormatPressure_Kpa_ConvertsFromPsi()
    {
        // 35 PSI ≈ 241 kPa
        var result = _sut.FormatPressure(35.0, TirePressureUnit.KPa);
        result.Should().Be("241 kPa");
    }

    [Fact]
    public void FormatPressure_Null_ReturnsDash()
    {
        _sut.FormatPressure(null, TirePressureUnit.Psi).Should().Be("--");
    }

    // === Efficiency ===

    [Fact]
    public void FormatEfficiency_MiPerKwh_ReturnsAsIs()
    {
        _sut.FormatEfficiency(3.2, DistanceUnit.Miles).Should().Be("3.2 mi/kWh");
    }

    [Fact]
    public void FormatEfficiency_KmPerKwh_ConvertsFromMiPerKwh()
    {
        // 3.0 mi/kWh ≈ 4.8 km/kWh
        var result = _sut.FormatEfficiency(3.0, DistanceUnit.Kilometers);
        result.Should().Be("4.8 km/kWh");
    }

    [Fact]
    public void FormatEfficiency_Null_ReturnsDash()
    {
        _sut.FormatEfficiency(null, DistanceUnit.Miles).Should().Be("--");
    }

    // === Currency ===

    [Fact]
    public void FormatCurrency_USD_UsesDollarSign()
    {
        _sut.FormatCurrency(12.50, "USD").Should().Be("$12.50");
    }

    [Fact]
    public void FormatCurrency_EUR_UsesEuroSign()
    {
        _sut.FormatCurrency(10.00, "EUR").Should().Be("\u20ac10.00");
    }

    [Fact]
    public void FormatCurrency_GBP_UsesPoundSign()
    {
        _sut.FormatCurrency(8.75, "GBP").Should().Be("\u00a38.75");
    }

    [Fact]
    public void FormatCurrency_Null_ReturnsDash()
    {
        _sut.FormatCurrency(null, "USD").Should().Be("--");
    }

    [Fact]
    public void FormatCurrency_UnknownCode_DefaultsToDollar()
    {
        _sut.FormatCurrency(5.00, "XYZ").Should().Be("$5.00");
    }

    // === Conversion round-trips ===

    [Fact]
    public void ConvertDistance_100Miles_Is_160934Km()
    {
        var km = _sut.ConvertDistance(100, DistanceUnit.Kilometers);
        km.Should().BeApproximately(160.934, 0.001);
    }

    [Fact]
    public void ConvertPressure_35Psi_Is_2413Bar()
    {
        var bar = _sut.ConvertPressure(35, TirePressureUnit.Bar);
        bar.Should().BeApproximately(2.413, 0.01);
    }

    [Fact]
    public void ConvertPressure_35Psi_Is_241Kpa()
    {
        var kpa = _sut.ConvertPressure(35, TirePressureUnit.KPa);
        kpa.Should().BeApproximately(241.3, 0.1);
    }

    // === Unit suffix helpers ===

    [Fact]
    public void GetTemperatureUnit_Fahrenheit()
    {
        _sut.GetTemperatureUnit(TemperatureUnit.Fahrenheit).Should().Be("\u00b0F");
    }

    [Fact]
    public void GetTemperatureUnit_Celsius()
    {
        _sut.GetTemperatureUnit(TemperatureUnit.Celsius).Should().Be("\u00b0C");
    }

    [Fact]
    public void GetDistanceUnit_Miles()
    {
        _sut.GetDistanceUnit(DistanceUnit.Miles).Should().Be("mi");
    }

    [Fact]
    public void GetDistanceUnit_Kilometers()
    {
        _sut.GetDistanceUnit(DistanceUnit.Kilometers).Should().Be("km");
    }

    [Fact]
    public void GetSpeedUnit_Miles()
    {
        _sut.GetSpeedUnit(DistanceUnit.Miles).Should().Be("mph");
    }

    [Fact]
    public void GetSpeedUnit_Kilometers()
    {
        _sut.GetSpeedUnit(DistanceUnit.Kilometers).Should().Be("km/h");
    }

    [Fact]
    public void GetPressureUnit_Psi()
    {
        _sut.GetPressureUnit(TirePressureUnit.Psi).Should().Be("PSI");
    }

    [Fact]
    public void GetPressureUnit_Bar()
    {
        _sut.GetPressureUnit(TirePressureUnit.Bar).Should().Be("bar");
    }

    [Fact]
    public void GetPressureUnit_Kpa()
    {
        _sut.GetPressureUnit(TirePressureUnit.KPa).Should().Be("kPa");
    }

    [Fact]
    public void GetEfficiencyUnit_Miles()
    {
        _sut.GetEfficiencyUnit(DistanceUnit.Miles).Should().Be("mi/kWh");
    }

    [Fact]
    public void GetEfficiencyUnit_Kilometers()
    {
        _sut.GetEfficiencyUnit(DistanceUnit.Kilometers).Should().Be("km/kWh");
    }
}
