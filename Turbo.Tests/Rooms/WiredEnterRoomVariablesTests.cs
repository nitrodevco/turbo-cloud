using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// User Enters Room runs its stack for the user who came in, through the room's own entry call:
/// the stack reads their name and what a permanent variable kept for them from their last
/// visit, while a value held only while in the room did not come back with them.
/// </summary>
public sealed class WiredEnterRoomVariablesTests
{
    private const int PLAYER = 300;
    private const int MARK = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredEnterRoomVariablesTests()
    {
        var fakes = _room.Harness.Fakes;

        fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call =>
        {
            var snapshot = (PlayerSummarySnapshot)call.Args[1]!;
            var avatar = new RoomPlayerAvatar
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                PlayerId = snapshot.PlayerId,
            };

            avatar.UpdateWithPlayer(snapshot);

            return avatar;
        };
    }

    [Theory]
    [InlineData(WiredAvailabilityType.Persistent, "E U=player V=7")]
    [InlineData(WiredAvailabilityType.UserActive, "E U=player V=null")]
    public async Task The_stack_reads_the_user_who_entered(
        WiredAvailabilityType availability,
        string logged
    )
    {
        var mark = await PlaceMarkAsync(availability);

        await EnterAsync();
        (await mark.GiveValueAsync(Key(mark), new WiredVariableValue(7), replace: true))
            .Should()
            .BeTrue();
        await _room.Harness.Room.RemoveAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer((PlayerId)PLAYER, 1),
            (PlayerId)PLAYER,
            Ct
        );
        await TickAsync(1);
        await BuildEnterStackAsync(mark, "E U=$(u) V=$(v)");

        await EnterAsync();
        await TickAsync(1);

        Wired.GetRoomLogs().Select(x => x.Message).Should().Equal(logged);
    }

    private WiredVariableKey Key(WiredVariableUser mark) =>
        new(
            mark.GetVarSnapshot().VariableId,
            WiredVariableTargetType.User,
            _room
                .Harness.Module<RoomAvatarModule>()
                .Players.Single(x => x.PlayerId == PLAYER)
                .ObjectId.Value
        );

    private Task EnterAsync() =>
        _room.Harness.Room.CreateAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer((PlayerId)PLAYER, _room.Harness.Room.RoomId),
            new PlayerSummarySnapshot
            {
                PlayerId = (PlayerId)PLAYER,
                Name = "player",
                Motto = string.Empty,
                Figure = string.Empty,
                Gender = default,
                AchievementScore = 0,
                BadgesRank = 0,
                IsOnline = true,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                RespectPoints = 0,
                RespectsLeft = 0,
                PetRespectsLeft = 0,
                RespectReplenishesLeft = 0,
            },
            RoomEntrySnapshot.Default,
            Ct
        );

    /// <summary>Enter Room writing <paramref name="text"/> to the logs, $(u) the user's name and $(v) their mark.</summary>
    private async Task BuildEnterStackAsync(WiredVariableUser mark, string text)
    {
        _room.AddBox<WiredTriggerHabboJoinRoom>(1, 0, 0, "wf_trg_enter_room");
        _room.AddBox<WiredActionWriteToLogs>(2, 0, 0, "wf_act_log");
        _room.AddBox<WiredAddonUsernamePlaceholder>(3, 0, 0, "wf_xtra_text_output_username");
        _room.AddBox<WiredAddonVariablePlaceholder>(4, 0, 0, "wf_xtra_text_output_variable");

        (await _room.SaveAsync<UpdateTriggerMessage>(1)).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                3,
                intParams: [0],
                stringParam: "u",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                4,
                intParams: [0, (int)WiredVariableTargetType.User, 0],
                stringParam: "v",
                variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: text,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        await StackChangedAsync(0, 0);
    }

    private async Task<WiredVariableUser> PlaceMarkAsync(WiredAvailabilityType availability)
    {
        var mark = _room.AddBox<WiredVariableUser>(MARK, 6, 6, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                MARK,
                intParams: [(int)availability, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await mark.LoadWiredAsync(Ct);
        await StackChangedAsync(6, 6);

        return mark;
    }

    private async Task StackChangedAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
