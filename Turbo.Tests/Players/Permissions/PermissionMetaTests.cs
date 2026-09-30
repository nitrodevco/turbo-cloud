using FluentAssertions;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class PermissionMetaTests
{
    [Theory]
    [InlineData(null, 50, 50)] // nothing sets it: the hotel default
    [InlineData("200", 50, 200)] // raised
    [InlineData("5", 50, 5)] // a group may lower it too
    [InlineData("0", 50, 0)] // zero is a limit
    [InlineData("-1", 50, 50)] // not a limit
    [InlineData("lots", 50, 50)]
    [InlineData("", 50, 50)]
    public void ReadLimit(string? value, int fallback, int expected)
    {
        PermissionMeta.ReadLimit(value, fallback).Should().Be(expected);
    }
}
