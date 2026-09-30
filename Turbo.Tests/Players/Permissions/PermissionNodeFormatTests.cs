using FluentAssertions;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class PermissionNodeFormatTests
{
    [Theory]
    [InlineData("")] // empty
    [InlineData("Room.enter")] // uppercase
    [InlineData("room..enter")] // empty segment
    [InlineData(".room")] // leading dot
    [InlineData("room.")] // trailing dot
    [InlineData("room.enter-locked")] // hyphen
    [InlineData("room.*")] // wildcard is not a node
    [InlineData("*")]
    public void IsValidNode_RejectsMalformed(string value)
    {
        PermissionNodeFormat.IsValidNode(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidNode_RejectsOverLength()
    {
        var value = new string('a', PermissionNodeFormat.MAX_LENGTH + 1);

        PermissionNodeFormat.IsValidNode(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("trade")]
    [InlineData("room.enter.locked")]
    [InlineData("perk.navigator.phase_two")]
    [InlineData("casino.table2.open")]
    public void IsValidNode_AcceptsDottedLowercase(string value)
    {
        PermissionNodeFormat.IsValidNode(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("room.*")]
    [InlineData("room.enter.*")]
    [InlineData("room.enter.locked")]
    public void IsValidAssignment_AcceptsNodesAndWildcards(string value)
    {
        PermissionNodeFormat.IsValidAssignment(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(".*")] // wildcard with no prefix other than the bare one
    [InlineData("room*")] // wildcard must follow a dot
    [InlineData("room.*.locked")] // wildcard only at the end
    [InlineData("room.**")]
    public void IsValidAssignment_RejectsMalformed(string value)
    {
        PermissionNodeFormat.IsValidAssignment(value).Should().BeFalse();
    }

    [Fact]
    public void Specificity_RanksExactThenLongestWildcard()
    {
        const string node = "room.enter.locked";

        var exact = PermissionNodeFormat.Specificity(node, node);
        var narrow = PermissionNodeFormat.Specificity("room.enter.*", node);
        var broad = PermissionNodeFormat.Specificity("room.*", node);
        var all = PermissionNodeFormat.Specificity("*", node);

        exact.Should().Be(PermissionNodeFormat.EXACT);
        exact.Should().BeGreaterThan(narrow);
        narrow.Should().BeGreaterThan(broad);
        broad.Should().BeGreaterThan(all);
        all.Should().Be(0);
    }

    [Theory]
    [InlineData("room.*", "room")] // a wildcard covers what is under its prefix, not the prefix
    [InlineData("room.*", "roomy.enter")] // prefix match stops at the dot
    [InlineData("room.enter.*", "room.control.any")]
    [InlineData("room.enter", "room.enter.locked")] // a node is not a prefix
    public void Specificity_DoesNotMatch(string assignment, string node)
    {
        PermissionNodeFormat
            .Specificity(assignment, node)
            .Should()
            .Be(PermissionNodeFormat.NO_MATCH);
    }
}
