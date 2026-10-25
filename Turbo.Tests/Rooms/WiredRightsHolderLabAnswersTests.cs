using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The user boxes on a rights holder as the room owner saw them on the official hotel (wired
/// matrix W3, LAB-W3 "wlabduo answers", official client captures 2026-10-24 with two accounts):
/// Kick throws a rights holder out, the effect first and the removal two seconds later; Freeze
/// wakes them before the freeze effect.
/// </summary>
public sealed class WiredRightsHolderLabAnswersTests
{
    private const int BOX = 2;
    private const int OWNER = 5;
    private const int OWNER_PLAYER = 1;
    private const int HOLDER = 6;
    private const int HOLDER_PLAYER = 100 + HOLDER;
    private const long FIRED_AT = 10_000;

    private readonly WiredRoom _room = new(8, 8);

    public WiredRightsHolderLabAnswersTests()
    {
        _room.Enter(OWNER, 1, 1, playerId: OWNER_PLAYER);
        _room.Enter(HOLDER, 3, 3);
        _room.Harness.State.PlayerIdsWithRights.Add(new PlayerId(HOLDER_PLAYER));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomObjectId[] Removed =>
        [.. _room.Harness.Fakes.SentToRoom<UserRemoveMessageComposer>().Select(x => x.ObjectId)];

    private int SessionsClosed => _room.Harness.Fakes.Log.Of("OnRemovedFromRoomAsync").Count();

    /// <summary>
    /// action.19.B1/R3 (15-49-44): Kick on a rights holder is allowed. The kicked user is
    /// whispered the text, the room sees AvatarEffect 4 at once and UserRemove 2.0 s later; the
    /// user does not walk to the door meanwhile.
    /// </summary>
    [Fact]
    public async Task Kick_takes_a_rights_holder_out_two_seconds_after_the_effect_action_19_B1()
    {
        var kick = _room.AddBox<WiredActionKickUser>(BOX, 0, 0, "wf_act_kick_user");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                stringParam: "wlabduo kick",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await Wired.ProcessWiredAsync(FIRED_AT, dormant: false, Ct);

        (await kick.ExecuteAsync(Triggered(HOLDER), Ct)).Should().BeTrue();

        _room
            .Harness.Fakes.SentToPlayer<WhisperMessageComposer>(HOLDER_PLAYER)
            .Select(x => x.Text)
            .Should()
            .Equal("wlabduo kick");
        _room
            .Harness.Fakes.SentToRoom<AvatarEffectMessageComposer>()
            .Select(x => (x.ObjectId, x.EffectId))
            .Should()
            .Equal(((RoomObjectId)HOLDER, 4));

        await Wired.ProcessWiredAsync(FIRED_AT + 1_500, dormant: false, Ct);
        Removed.Should().BeEmpty();
        SessionsClosed.Should().Be(0);

        await Wired.ProcessWiredAsync(FIRED_AT + 2_000, dormant: false, Ct);
        Removed.Should().Equal((RoomObjectId)HOLDER);
        SessionsClosed.Should().Be(1);
    }

    /// <summary>
    /// action.31.B1 (15-48-31): Freeze on an awake rights holder; the owner's client gets Sleep
    /// false and then AvatarEffect 218.
    /// </summary>
    [Fact]
    public async Task Freeze_wakes_a_rights_holder_before_the_effect_action_31_B1()
    {
        var freeze = _room.AddBox<WiredActionFreezeUser>(BOX, 0, 0, "wf_act_freeze");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0, 0],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        (await freeze.ExecuteAsync(Triggered(HOLDER), Ct)).Should().BeTrue();
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(10_000, dormant: false, Ct);

        _room
            .Harness.Fakes.SentToRoom<IComposer>()
            .Where(x => x is SleepMessageComposer or AvatarEffectMessageComposer)
            .Should()
            .Equal(
                new SleepMessageComposer { ObjectId = HOLDER, IsSleeping = false },
                new AvatarEffectMessageComposer
                {
                    ObjectId = HOLDER,
                    EffectId = 218,
                    DelayMilliseconds = 0,
                }
            );
    }

    private WiredExecutionContext Triggered(int objectId) =>
        new(_room.Harness.Room)
        {
            Selected = new WiredSelectionSet([], [(RoomObjectId)objectId]),
            CancellationToken = Ct,
        };
}
