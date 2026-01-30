using FluentAssertions;
using RivianMate.Api.Helpers;
using RivianMate.Tests.TestHelpers;
using Xunit;

namespace RivianMate.Tests.Helpers;

public class DateTimeFormatHelperTests
{
    [Fact]
    public void FormatDuration_ReturnsMinutes_WhenUnderOneHour()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 10, 45, 0, DateTimeKind.Utc);

        DateTimeFormatHelper.FormatDuration(start, end).Should().Be("45 min");
    }

    [Fact]
    public void FormatDuration_ReturnsHoursAndMinutes_WhenOverOneHour()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 12, 15, 0, DateTimeKind.Utc);

        DateTimeFormatHelper.FormatDuration(start, end).Should().Be("2h 15m");
    }

    [Fact]
    public void FormatDuration_ReturnsInProgress_WhenEndIsNull()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        DateTimeFormatHelper.FormatDuration(start, null).Should().Be("In progress");
    }

    [Fact]
    public void FormatRelativeDate_ReturnsToday_WhenSameDay()
    {
        var tz = MockTimeZoneService.CreateUtc();
        var now = DateTime.UtcNow;

        DateTimeFormatHelper.FormatRelativeDate(now, tz).Should().Be("Today");
    }

    [Fact]
    public void FormatRelativeDate_ReturnsYesterday_WhenPreviousDay()
    {
        var tz = MockTimeZoneService.CreateUtc();
        var yesterday = DateTime.UtcNow.AddDays(-1);

        DateTimeFormatHelper.FormatRelativeDate(yesterday, tz).Should().Be("Yesterday");
    }

    [Fact]
    public void FormatRelativeDate_ReturnsFormattedDate_WhenOlder()
    {
        var tz = MockTimeZoneService.CreateUtc();
        var oldDate = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var result = DateTimeFormatHelper.FormatRelativeDate(oldDate, tz);
        result.Should().Be("Jun 15, 2025");
    }

    [Fact]
    public void FormatRelativeTime_ReturnsJustNow_WhenWithin60Seconds()
    {
        var tz = MockTimeZoneService.CreateUtc();
        var recent = DateTime.UtcNow.AddSeconds(-30);

        DateTimeFormatHelper.FormatRelativeTime(recent, tz).Should().Be("Just now");
    }

    [Fact]
    public void FormatRelativeTime_ReturnsMinutesAgo_WhenWithinHour()
    {
        var tz = MockTimeZoneService.CreateUtc();
        var minutesAgo = DateTime.UtcNow.AddMinutes(-15);

        DateTimeFormatHelper.FormatRelativeTime(minutesAgo, tz).Should().Be("15m ago");
    }

    [Fact]
    public void FormatRelativeTime_ReturnsHoursAgo_WhenWithinDay()
    {
        var tz = MockTimeZoneService.CreateUtc();
        var hoursAgo = DateTime.UtcNow.AddHours(-3);

        DateTimeFormatHelper.FormatRelativeTime(hoursAgo, tz).Should().Be("3h ago");
    }

    [Fact]
    public void FormatDateHeader_ReturnsToday_WhenSameDate()
    {
        var today = DateTime.Today;
        DateTimeFormatHelper.FormatDateHeader(today, today).Should().Be("Today");
    }

    [Fact]
    public void FormatDateHeader_ReturnsYesterday_WhenPreviousDate()
    {
        var today = DateTime.Today;
        DateTimeFormatHelper.FormatDateHeader(today.AddDays(-1), today).Should().Be("Yesterday");
    }

    [Fact]
    public void FormatDateHeader_ReturnsDayName_WhenWithinWeek()
    {
        var today = DateTime.Today;
        var threeDaysAgo = today.AddDays(-3);
        var result = DateTimeFormatHelper.FormatDateHeader(threeDaysAgo, today);
        result.Should().Be(threeDaysAgo.ToString("dddd"));
    }

    [Fact]
    public void FormatDateHeader_ReturnsFullDate_WhenOlderThanWeek()
    {
        var today = DateTime.Today;
        var oldDate = today.AddDays(-10);
        var result = DateTimeFormatHelper.FormatDateHeader(oldDate, today);
        result.Should().Be(oldDate.ToString("MMMM d, yyyy"));
    }

    [Fact]
    public void FormatTimeOnly_ReturnsCorrectFormat()
    {
        var tz = MockTimeZoneService.CreateUtc();
        var time = new DateTime(2026, 1, 15, 15, 30, 0, DateTimeKind.Utc);

        var result = DateTimeFormatHelper.FormatTimeOnly(time, tz);
        result.Should().Be("3:30 PM");
    }
}
