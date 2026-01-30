using FluentAssertions;
using RivianMate.Api.Services;
using Xunit;

namespace RivianMate.Tests.Services;

public class UserLocationServiceMathTests
{
    // === CalculateDistanceMeters ===

    [Fact]
    public void CalculateDistanceMeters_ReturnsZero_ForSamePoint()
    {
        var result = UserLocationService.CalculateDistanceMeters(40.7128, -74.0060, 40.7128, -74.0060);
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateDistanceMeters_ReturnsCorrect_ForKnownDistances()
    {
        // NYC to London ≈ 5,570 km
        var result = UserLocationService.CalculateDistanceMeters(40.7128, -74.0060, 51.5074, -0.1278);
        var distanceKm = result / 1000;
        distanceKm.Should().BeApproximately(5570, 50);
    }

    [Fact]
    public void CalculateDistanceMeters_ReturnsCorrect_AcrossEquator()
    {
        // Quito, Ecuador (0.1807, -78.4678) to Bogota, Colombia (4.7110, -74.0721) ≈ 710 km
        var result = UserLocationService.CalculateDistanceMeters(0.1807, -78.4678, 4.7110, -74.0721);
        var distanceKm = result / 1000;
        distanceKm.Should().BeApproximately(710, 30);
    }

    [Fact]
    public void CalculateDistanceMeters_ReturnsCorrect_ShortDistance()
    {
        // Two points about 100m apart
        var result = UserLocationService.CalculateDistanceMeters(40.7484, -73.9857, 40.7493, -73.9857);
        result.Should().BeApproximately(100, 15);
    }

    [Fact]
    public void CalculateDistanceMeters_AcrossDateLine()
    {
        // Tokyo (35.6762, 139.6503) to Honolulu (21.3069, -157.8583) ≈ 6,200 km
        var result = UserLocationService.CalculateDistanceMeters(35.6762, 139.6503, 21.3069, -157.8583);
        var distanceKm = result / 1000;
        distanceKm.Should().BeApproximately(6200, 100);
    }
}
