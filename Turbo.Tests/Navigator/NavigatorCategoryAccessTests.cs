using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Xunit;

namespace Turbo.Tests.Navigator;

public class NavigatorCategoryAccessTests
{
    [Fact]
    public void RetroDefaultMinRank_IsEveryones()
    {
        NavigatorCategoryAccess
            .CanSee(Category(minRank: 1), Holds(), SecurityLevelType.None)
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(SecurityLevelType.None, false)]
    [InlineData(SecurityLevelType.Employee, false)]
    [InlineData(SecurityLevelType.Moderator, true)]
    [InlineData(SecurityLevelType.Administrator, true)]
    public void MinRank_ComparesWithSecurityLevel(SecurityLevelType level, bool expected)
    {
        NavigatorCategoryAccess.CanSee(Category(minRank: 5), Holds(), level).Should().Be(expected);
    }

    [Fact]
    public void StaffOnly_NeedsTheStaffCategoryNode_NotJustALevel()
    {
        var category = Category(staffOnly: true);

        NavigatorCategoryAccess
            .CanSee(category, Holds(), SecurityLevelType.Administrator)
            .Should()
            .BeFalse();
        NavigatorCategoryAccess
            .CanSee(
                category,
                Holds(PermissionNodes.Navigator.CATEGORY_STAFF),
                SecurityLevelType.Community
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public void RequiredNode_MustBeHeld()
    {
        var category = Category(requiredNode: "casino.vip");

        NavigatorCategoryAccess
            .CanSee(category, Holds(), SecurityLevelType.None)
            .Should()
            .BeFalse();
        NavigatorCategoryAccess
            .CanSee(category, Holds("casino.vip"), SecurityLevelType.None)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Invisible_IsNobodys()
    {
        NavigatorCategoryAccess
            .CanSee(
                Category(visible: false),
                Holds(PermissionNodes.Navigator.CATEGORY_STAFF),
                SecurityLevelType.Administrator
            )
            .Should()
            .BeFalse();
    }

    private static NavigatorFlatCategorySnapshot Category(
        int minRank = 1,
        bool staffOnly = false,
        bool visible = true,
        string? requiredNode = null
    ) =>
        new()
        {
            Id = 1,
            Name = "test",
            Visible = visible,
            Automatic = false,
            AutomaticCategoryKey = string.Empty,
            GlobalCategoryKey = string.Empty,
            StaffOnly = staffOnly,
            MinRank = minRank,
            OrderNum = 0,
            RequiredNode = requiredNode,
        };

    private static ResolvedPermissionsSnapshot Holds(params string[] nodes) =>
        ResolvedPermissionsSnapshot.EMPTY with
        {
            Granted = [.. nodes],
        };
}
