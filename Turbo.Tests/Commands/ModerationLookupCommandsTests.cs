using System.Net;
using FluentAssertions;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Operations.Commands;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// What a moderator asks in game before acting on a player: what has been done to them before
/// (<c>:history</c>), and which accounts are theirs (<c>:alts</c>). Neither could be answered
/// without the panel, and the second not at all: the logins were kept and never read.
/// </summary>
public sealed class ModerationLookupCommandsTests : OperatorCommandsTestBase
{
    private static readonly DateTime T0 = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    public ModerationLookupCommandsTests()
    {
        foreach (
            var (id, name) in new[]
            {
                (STAFF, "staff"),
                (ALICE, "Alice"),
                (BOB, "Bob"),
                (CAROL, "Carol"),
            }
        )
            Db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                }
            );

        Hotel.Commands.Register([new HistoryCommand(Db), new AltsCommand(Db)]);
    }

    [Fact]
    public async Task History_lists_what_was_done_newest_first_by_whom_and_why()
    {
        Db.Insert(Action(1, ModerationActionType.Warn, STAFF, ALICE, T0, null));
        Db.Insert(Action(2, ModerationActionType.Silence, STAFF, ALICE, T0.AddDays(1), "spam"));
        Db.Insert(
            Action(
                3,
                ModerationActionType.RoomFloodMute,
                null,
                ALICE,
                T0.AddDays(2),
                "flood protection"
            )
        );
        Db.Insert(Action(4, ModerationActionType.Ban, STAFF, BOB, T0, "not Alice"));
        var staff = Staff(PermissionNodes.Command.HISTORY);

        await Hotel.RunAsync("history", staff, "alice");

        staff
            .Notices.Should()
            .ContainSingle()
            .Which.Should()
            .Equal(
                "Alice: the last 3, newest first",
                "2026-09-03 12:00 RoomFloodMute by hotel: flood protection",
                "2026-09-02 12:00 Silence by staff: spam",
                "2026-09-01 12:00 Warn by staff"
            );
    }

    [Fact]
    public async Task History_of_a_player_with_none_says_so()
    {
        var staff = Staff(PermissionNodes.Command.HISTORY);

        await Hotel.RunAsync("history", staff, "carol");

        staff.Replies.Should().Equal("Carol has no moderation history.");
    }

    [Fact]
    public async Task Alts_lists_a_shared_machine_first_and_never_shows_an_address()
    {
        Db.Insert(Login(1, ALICE, "10.0.0.1", "machine-a"));
        Db.Insert(Login(2, BOB, "10.0.0.1", "machine-b"));
        Db.Insert(Login(3, CAROL, "10.9.9.9", "machine-a"));
        Db.Insert(
            new PlayerSanctionEntity
            {
                Id = 1,
                PlayerEntityId = CAROL,
                Kind = SanctionKind.Ban,
                Reason = "cheating",
            }
        );
        var staff = Staff(PermissionNodes.Command.ALTS);

        await Hotel.RunAsync("alts", staff, "alice");

        var notice = staff.Notices.Should().ContainSingle().Subject;
        notice
            .Should()
            .Equal(
                "Accounts sharing with Alice:",
                $"Carol: same machine, last {DateTime.UtcNow:yyyy-MM-dd} (banned)",
                $"Bob: 1 address, last {DateTime.UtcNow:yyyy-MM-dd}"
            );
        notice.Should().NotContain(x => x.Contains("10.0.0.1"));
    }

    [Fact]
    public async Task Alts_needs_its_own_node()
    {
        var staff = Staff(PermissionNodes.Command.HISTORY);

        await Hotel.RunAsync("alts", staff, "alice");

        staff.Notices.Should().BeEmpty();
    }

    private static PlayerLoginEntity Login(int id, int player, string address, string machine) =>
        new()
        {
            Id = id,
            PlayerEntityId = player,
            IpAddress = IPAddress.Parse(address),
            MachineId = machine,
        };

    private static ModerationActionEntity Action(
        long id,
        ModerationActionType action,
        int? actor,
        int target,
        DateTime at,
        string? reason
    ) =>
        new()
        {
            Id = id,
            Action = action,
            Source = actor is null ? ModerationSource.System : ModerationSource.Game,
            ActorId = actor,
            TargetPlayerId = target,
            Reason = reason,
            CreatedAt = at,
        };
}
