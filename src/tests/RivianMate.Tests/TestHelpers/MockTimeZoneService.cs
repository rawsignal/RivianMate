using Microsoft.JSInterop;
using Moq;
using RivianMate.Api.Services;

namespace RivianMate.Tests.TestHelpers;

/// <summary>
/// Creates a TimeZoneService that uses UTC for all conversions (no JS interop needed).
/// </summary>
public static class MockTimeZoneService
{
    public static TimeZoneService CreateUtc()
    {
        var mockJs = new Mock<IJSRuntime>();
        var tz = new TimeZoneService(mockJs.Object);
        // Explicitly set UTC timezone (without JS interop)
        tz.SetUserPreferredTimeZone("UTC");
        return tz;
    }
}
