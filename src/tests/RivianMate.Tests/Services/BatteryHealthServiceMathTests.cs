using FluentAssertions;
using RivianMate.Api.Services;
using Xunit;

namespace RivianMate.Tests.Services;

public class BatteryHealthServiceMathTests
{
    // === CalculateReadingConfidence ===

    [Fact]
    public void CalculateReadingConfidence_ReturnsHighConfidence_WhenIdealConditions()
    {
        // High SoC (>=90) + optimal temp (15-30°C) → 0.5 + 0.3 + 0.2 = 1.0
        var result = BatteryHealthService.CalculateReadingConfidence(95, 22);
        result.Should().Be(1.0);
    }

    [Fact]
    public void CalculateReadingConfidence_ReturnsModerateConfidence_WhenModerateSoc()
    {
        // SoC 50-70 + optimal temp → 0.5 + 0.1 + 0.2 = 0.8
        var result = BatteryHealthService.CalculateReadingConfidence(55, 20);
        result.Should().Be(0.8);
    }

    [Fact]
    public void CalculateReadingConfidence_ReturnsLowerConfidence_WhenLowSoc()
    {
        // SoC < 20 + optimal temp → 0.5 - 0.2 + 0.2 = 0.5
        var result = BatteryHealthService.CalculateReadingConfidence(10, 20);
        result.Should().Be(0.5);
    }

    [Fact]
    public void CalculateReadingConfidence_ReturnsLowerConfidence_WhenExtremeTemp()
    {
        // High SoC + extreme temp (>40) → 0.5 + 0.3 - 0.2 = 0.6
        var result = BatteryHealthService.CalculateReadingConfidence(95, 45);
        result.Should().BeApproximately(0.6, 1e-10);
    }

    [Fact]
    public void CalculateReadingConfidence_ReturnsLowerConfidence_WhenCold()
    {
        // High SoC + cold (<5°C) → 0.5 + 0.3 - 0.2 = 0.6
        var result = BatteryHealthService.CalculateReadingConfidence(95, 0);
        result.Should().BeApproximately(0.6, 1e-10);
    }

    [Fact]
    public void CalculateReadingConfidence_HandlesNullSoc()
    {
        // null SoC + optimal temp → 0.5 + 0.2 = 0.7
        var result = BatteryHealthService.CalculateReadingConfidence(null, 20);
        result.Should().Be(0.7);
    }

    [Fact]
    public void CalculateReadingConfidence_HandlesNullTemp()
    {
        // High SoC + null temp → 0.5 + 0.3 = 0.8
        var result = BatteryHealthService.CalculateReadingConfidence(95, null);
        result.Should().Be(0.8);
    }

    [Fact]
    public void CalculateReadingConfidence_HandlesAllNull()
    {
        // Both null → base confidence 0.5
        var result = BatteryHealthService.CalculateReadingConfidence(null, null);
        result.Should().Be(0.5);
    }

    [Fact]
    public void CalculateReadingConfidence_ClampsToMinimum()
    {
        // Low SoC + extreme temp → 0.5 - 0.2 - 0.2 = 0.1 (clamped min)
        var result = BatteryHealthService.CalculateReadingConfidence(10, 50);
        result.Should().Be(0.1);
    }

    // === CalculateWeightedLinearRegression ===

    [Fact]
    public void CalculateWeightedLinearRegression_ReturnsCorrectSlope_ForLinearData()
    {
        // Perfect linear data: health = 100 - 0.001 * odometer
        var points = new List<(double Odometer, double Health, double Confidence)>
        {
            (0, 100, 1.0),
            (10000, 90, 1.0),
            (20000, 80, 1.0),
            (30000, 70, 1.0)
        };

        var (slope, intercept) = BatteryHealthService.CalculateWeightedLinearRegression(points);

        slope.Should().BeApproximately(-0.001, 0.0001);
        intercept.Should().BeApproximately(100, 0.1);
    }

    [Fact]
    public void CalculateWeightedLinearRegression_HandlesEqualWeights()
    {
        var points = new List<(double Odometer, double Health, double Confidence)>
        {
            (1000, 99, 0.5),
            (5000, 97, 0.5),
            (10000, 95, 0.5)
        };

        var (slope, intercept) = BatteryHealthService.CalculateWeightedLinearRegression(points);

        // Slope should be negative (health decreasing)
        slope.Should().BeNegative();
        intercept.Should().BeGreaterThan(95);
    }

    [Fact]
    public void CalculateWeightedLinearRegression_HandlesSinglePoint()
    {
        // Single point: denominator = 0, should fallback
        var points = new List<(double Odometer, double Health, double Confidence)>
        {
            (5000, 97, 0.8)
        };

        // With a single point, the denominator sumW*sumWX2 - sumWX*sumWX = 0
        // because there's only one point: W*WX2 = W*W*X2 and WX*WX = W2*X2
        // So it should return slope=0 and intercept=average health
        var (slope, intercept) = BatteryHealthService.CalculateWeightedLinearRegression(points);

        slope.Should().Be(0);
        intercept.Should().Be(97);
    }

    [Fact]
    public void CalculateWeightedLinearRegression_WeightsHighConfidenceMore()
    {
        // Two clusters: low confidence high readings, high confidence lower readings
        var points = new List<(double Odometer, double Health, double Confidence)>
        {
            (1000, 100, 0.1),  // Low confidence, high health
            (2000, 100, 0.1),
            (3000, 95, 1.0),   // High confidence, lower health
            (4000, 93, 1.0)
        };

        var (slope, intercept) = BatteryHealthService.CalculateWeightedLinearRegression(points);

        // The regression should be pulled more toward the high-confidence points
        // At odometer 3500, health should be closer to 94 than 98
        var healthAt3500 = intercept + slope * 3500;
        healthAt3500.Should().BeLessThan(98);
    }
}
