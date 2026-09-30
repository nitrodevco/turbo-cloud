using System;
using FluentAssertions;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class PermissionExpiryTests
{
    private static readonly DateTime NOW = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Extend_AddsToWhatIsLeft()
    {
        var running = NOW.AddDays(10);

        PermissionExpiry
            .Resolve(NOW.AddDays(30), running, PermissionExpiryModeType.Extend, NOW)
            .Should()
            .Be(NOW.AddDays(40));
    }

    [Fact]
    public void Extend_OfLapsedAssignment_StartsFromNow()
    {
        PermissionExpiry
            .Resolve(NOW.AddDays(30), NOW.AddDays(-1), PermissionExpiryModeType.Extend, NOW)
            .Should()
            .Be(NOW.AddDays(30));
    }

    [Fact]
    public void Extend_WithNothingRunning_IsTheRequestedExpiry()
    {
        PermissionExpiry
            .Resolve(NOW.AddDays(30), null, PermissionExpiryModeType.Extend, NOW)
            .Should()
            .Be(NOW.AddDays(30));
    }

    [Fact]
    public void Replace_IgnoresWhatIsLeft()
    {
        PermissionExpiry
            .Resolve(NOW.AddDays(30), NOW.AddDays(10), PermissionExpiryModeType.Replace, NOW)
            .Should()
            .Be(NOW.AddDays(30));
    }

    [Fact]
    public void Permanent_StaysPermanent()
    {
        PermissionExpiry
            .Resolve(null, NOW.AddDays(10), PermissionExpiryModeType.Extend, NOW)
            .Should()
            .BeNull();
    }
}
