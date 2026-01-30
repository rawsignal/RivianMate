using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using RivianMate.Api.Services;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Tests.TestHelpers;
using Xunit;

namespace RivianMate.Tests.Services;

public class UserPreferencesServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private UserPreferencesService CreateService(string? dbName = null)
    {
        var factory = DbContextHelper.CreateFactory(dbName);
        var logger = new Mock<ILogger<UserPreferencesService>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new UserPreferencesService(factory, logger.Object, cache);
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsDefaults_WhenNotInDb()
    {
        var service = CreateService();

        var result = await service.GetPreferencesAsync(_userId);

        result.Should().NotBeNull();
        result.UserId.Should().Be(_userId);
        result.DistanceUnit.Should().Be(DistanceUnit.Miles);
        result.TemperatureUnit.Should().Be(TemperatureUnit.Fahrenheit);
        result.TirePressureUnit.Should().Be(TirePressureUnit.Psi);
        result.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsFromCache_WhenCached()
    {
        var dbName = Guid.NewGuid().ToString();
        var service = CreateService(dbName);

        // First call - populates cache
        var first = await service.GetPreferencesAsync(_userId);

        // Second call - should come from cache (same service instance = same cache)
        var second = await service.GetPreferencesAsync(_userId);

        second.Should().NotBeNull();
        second.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetPreferencesAsync_QueriesDb_WhenNotCached()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed the database with preferences
        using (var db = DbContextHelper.CreateInMemory(dbName))
        {
            db.UserPreferences.Add(new UserPreferences
            {
                UserId = _userId,
                DistanceUnit = DistanceUnit.Kilometers,
                TemperatureUnit = TemperatureUnit.Celsius,
                TirePressureUnit = TirePressureUnit.Bar,
                CurrencyCode = "EUR"
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(dbName);
        var result = await service.GetPreferencesAsync(_userId);

        result.DistanceUnit.Should().Be(DistanceUnit.Kilometers);
        result.TemperatureUnit.Should().Be(TemperatureUnit.Celsius);
        result.TirePressureUnit.Should().Be(TirePressureUnit.Bar);
        result.CurrencyCode.Should().Be("EUR");
    }

    [Fact]
    public async Task SavePreferencesAsync_CreatesNewRecord_WhenNoneExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var service = CreateService(dbName);

        var preferences = new UserPreferences
        {
            UserId = _userId,
            DistanceUnit = DistanceUnit.Kilometers,
            TemperatureUnit = TemperatureUnit.Celsius,
            TirePressureUnit = TirePressureUnit.KPa,
            CurrencyCode = "GBP"
        };

        await service.SavePreferencesAsync(preferences);

        // Verify it was persisted
        using var db = DbContextHelper.CreateInMemory(dbName);
        var saved = db.UserPreferences.FirstOrDefault(p => p.UserId == _userId);
        saved.Should().NotBeNull();
        saved!.DistanceUnit.Should().Be(DistanceUnit.Kilometers);
        saved.CurrencyCode.Should().Be("GBP");
    }

    [Fact]
    public async Task SavePreferencesAsync_UpdatesExisting_WhenExists()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed existing preferences
        using (var db = DbContextHelper.CreateInMemory(dbName))
        {
            db.UserPreferences.Add(new UserPreferences
            {
                UserId = _userId,
                DistanceUnit = DistanceUnit.Miles,
                TemperatureUnit = TemperatureUnit.Fahrenheit,
                CurrencyCode = "USD"
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(dbName);

        var updated = new UserPreferences
        {
            UserId = _userId,
            DistanceUnit = DistanceUnit.Kilometers,
            TemperatureUnit = TemperatureUnit.Celsius,
            CurrencyCode = "CAD"
        };

        await service.SavePreferencesAsync(updated);

        // Verify the update
        using var verifyDb = DbContextHelper.CreateInMemory(dbName);
        var saved = verifyDb.UserPreferences.FirstOrDefault(p => p.UserId == _userId);
        saved.Should().NotBeNull();
        saved!.DistanceUnit.Should().Be(DistanceUnit.Kilometers);
        saved.CurrencyCode.Should().Be("CAD");
    }

    [Fact]
    public async Task SavePreferencesAsync_InvalidatesCache()
    {
        var dbName = Guid.NewGuid().ToString();
        var service = CreateService(dbName);

        // First get - populates cache with defaults
        var defaults = await service.GetPreferencesAsync(_userId);
        defaults.DistanceUnit.Should().Be(DistanceUnit.Miles);

        // Save new preferences - should invalidate cache
        var updated = new UserPreferences
        {
            UserId = _userId,
            DistanceUnit = DistanceUnit.Kilometers,
            TemperatureUnit = TemperatureUnit.Celsius,
            CurrencyCode = "USD"
        };
        await service.SavePreferencesAsync(updated);

        // Next get should read from DB, not cache
        var result = await service.GetPreferencesAsync(_userId);
        result.DistanceUnit.Should().Be(DistanceUnit.Kilometers);
    }
}
