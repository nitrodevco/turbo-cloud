using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Leaderboard;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIRED Variable Leaderboard (add-on 1005, released 2026-10-13; client code in
/// WIN63-202610121709, unchanged in WIN63-202610131318). On room entry the client sends
/// <c>VariableLeaderboardReplaceSubscription</c> (0, 0, "0", []) and Habbo answers with
/// <c>VariableLeaderboardSnapshot</c>, empty when the room has none (official client capture:
/// out <c>000000000000000000013000000000</c>, in <c>00000000</c>). A published leaderboard is in
/// the snapshot (<c>VariableLeaderboardDescriptorData.fromMessage</c>); a subscription to one of
/// its chunks is answered with the ranked rows (<c>AchievementResolutionProgressMessageParser</c>,
/// rows <c>variableleaderboard/_-02R.as</c>). Settings: <c>VariableLeaderboardConfigCodec.as</c>.
/// </summary>
public sealed class WiredVariableLeaderboardTests
{
    private const int VARIABLE = 10;
    private const int LEADERBOARD = 11;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Room_entry_is_answered_with_an_empty_snapshot()
    {
        _room.Enter(1, 1, 1);

        var replies = await SubscribeAsync(Hex("000000000000000000013000000000"), player: 1);

        replies.Should().ContainSingle();
        replies[0]
            .Header.Should()
            .Be(PacketHarness.Outgoing("VariableLeaderboardSnapshotMessageComposer"));
        HexOf(replies[0]).Should().Be("00000000");
    }

    [Fact]
    public async Task A_published_leaderboard_is_in_the_snapshot()
    {
        var variableId = await BuildPublishedAsync();

        var reply = (await SubscribeAsync(RoomOpen(), player: 1)).Single();

        reply.PopInt().Should().Be(1, "one leaderboard");
        reply.PopInt().Should().Be(1, "Habbo numbers a room's leaderboards from 1");
        var revision = reply.PopInt();
        revision.Should().Be(1, "the official capture's first revision");
        reply.PopString().Should().Be("Richest");
        reply.PopString().Should().Be("Who has the most coins");
        reply.PopInt().Should().Be(3);
        reply.PopInt().Should().Be(4);
        reply.PopInt().Should().Be(-5);
        reply.PopInt().Should().Be(2);
        reply.PopInt().Should().Be(0, "highest value first");
        reply.PopInt().Should().Be(2, "two columns");
        reply.PopInt().Should().Be(0);
        reply.PopString().Should().Be("Username");
        reply.PopInt().Should().Be(3);
        reply.PopInt().Should().Be(1);
        reply.PopString().Should().Be("Coins");
        reply.PopInt().Should().Be(0);
        reply.PopInt().Should().Be(1, "one stacked persisted variable");
        reply.PopString().Should().Be(variableId);
        reply.PopString().Should().Be("Total Coins", "the suggested name of total_coins");
        reply.PopString().Should().Be(variableId, "the default is the first choice");
        reply.PopInt().Should().Be(1, "page size option 15");
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task A_chunk_ranks_the_holders_by_value()
    {
        var variableId = await BuildPublishedAsync();
        var revision = await RevisionAsync();

        var chunk = (
            await SubscribeAsync(Subscription(1, revision, variableId, 0), player: 1)
        ).Single();

        chunk
            .Header.Should()
            .Be(PacketHarness.Outgoing("VariableLeaderboardChunkUpdateMessageComposer"));
        chunk.PopInt().Should().Be(1);
        chunk.PopInt().Should().Be(revision);
        chunk.PopString().Should().Be(variableId);
        chunk.PopInt().Should().Be(0, "chunk 0");
        chunk.PopInt().Should().Be(3, "three holders");
        chunk.PopInt().Should().Be(3, "three rows");

        var ranked = new List<(int Rank, int UserId, string Value)>();

        for (var i = 0; i < 3; i++)
        {
            var rank = chunk.PopInt();
            var userId = chunk.PopInt();

            chunk.PopString();
            chunk.PopString();

            var value = chunk.PopString();

            chunk.PopLong().Should().BePositive("creation time");
            chunk.PopLong().Should().BePositive("last update");
            chunk.PopBoolean().Should().BeFalse("no level column");
            chunk.PopBoolean().Should().BeFalse("no text column");
            ranked.Add((rank, userId, value));
        }

        chunk.End.Should().BeTrue();
        ranked.Should().Equal((1, 102, "20"), (2, 103, "10"), (3, 101, "5"));
    }

    /// <summary>
    /// The Text column shows the label a text connector on the stack gives each value, also when
    /// the connector is put there after the variable (r55 tester report: "-" for a holder of 10
    /// under a connector "0=Test0 10=Test10" until the room was reloaded). Viewers get the
    /// labelled rows without subscribing again.
    /// </summary>
    [Fact]
    public async Task The_text_column_takes_a_connector_added_after_the_variable()
    {
        int[] usernameAndText =
        [
            0,
            2,
            0,
            VariableLeaderboardConfig.COLUMN_TEXT,
            -1,
            -1,
            .. Ints()[6..],
        ];
        var variableId = await BuildPublishedAsync(usernameAndText);
        var revision = await RevisionAsync();

        await SubscribeAsync(Subscription(1, revision, variableId, 0), player: 1);
        await _room.Harness.Room.VariableLeaderboardSystem.ProcessAsync(15_000, Ct);

        _room.AddBox<WiredAddonVariableTextConnector>(12, 3, 3, "wf_xtra_var_text_connector");
        (await _room.SaveAsync<UpdateAddonMessage>(12, stringParam: "5=Five\n10=Ten\n20=Twenty"))
            .Should()
            .BeTrue();
        await Wired.ProcessWiredAsync(20_000, dormant: false, Ct);
        await _room.Harness.Room.VariableLeaderboardSystem.ProcessAsync(20_000, Ct);

        var pushed = _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Method == "SendComposerAsync" && x.Key is long key && key == 101
            )
            .SelectMany(x => x.Args.OfType<VariableLeaderboardChunkUpdateMessageComposer>())
            .LastOrDefault();

        pushed.Should().NotBeNull("the viewer's chunk changed");
        pushed!.Rows.Select(x => x.TextConnection).Should().Equal("Twenty", "Ten", "Five");
    }

    [Fact]
    public async Task A_subscription_for_an_old_revision_gets_nothing()
    {
        var variableId = await BuildPublishedAsync();
        var revision = await RevisionAsync();

        (await SubscribeAsync(Subscription(1, revision + 1, variableId, 0), player: 1))
            .Should()
            .BeEmpty();
    }

    /// <summary>
    /// A box that was never saved opens with the codec's unpublished settings as its ints and as
    /// its defaults (official client capture of a fresh box, WIN63-202610151745: intParams and
    /// defaultIntParams both [0,2,0,1,-1,-1,0,0,0,0,0,0,0,0,3,0,0,0,0], 12 x "0", string ""),
    /// which is <c>VariableLeaderboardConfigCodec.encode(decodeUnpublished())</c>.
    /// </summary>
    [Fact]
    public async Task A_fresh_box_opens_with_the_official_unpublished_ints()
    {
        int[] official = [0, 2, 0, 1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0];
        var leaderboard = _room.AddBox<WiredAddonVariableLeaderboard>(
            LEADERBOARD,
            3,
            3,
            "wf_xtra_var_leaderboard"
        );

        await leaderboard.LoadWiredAsync(Ct);

        var snapshot = await Wired.GetBoxSnapshotAsync((RoomObjectId)LEADERBOARD, Ct);

        snapshot!.IntParams.Should().Equal(official);
        snapshot.DefaultIntParams.Should().Equal(official);
        snapshot.StringParam.Should().BeEmpty();
        snapshot.VariableIds.Select(id => id.ToString()).Should().Equal(NoVariables());
        leaderboard.IsPublished.Should().BeFalse();

        var bytes = HexOf(
            PacketHarness.Encode(new WiredFurniAddonEventMessageComposer { WiredData = snapshot })
        );

        // The message ends with the default ints, as in the capture.
        bytes
            .Should()
            .EndWith("00000013" + string.Concat(official.Select(value => value.ToString("X8"))));
    }

    [Fact]
    public async Task A_configuration_the_client_would_not_send_is_refused()
    {
        await BuildStackAsync();

        var result = await _room.SaveWithResultAsync<UpdateAddonMessage>(
            LEADERBOARD,
            intParams: Ints(),
            stringParam: Strings(title: ""),
            variableIds: NoVariables()
        );

        result.IsSaved.Should().BeFalse();
        result.ErrorKey.Should().Be("wiredfurni.error.variable_leaderboard.invalid_configuration");
    }

    [Fact]
    public async Task The_editor_gets_the_leaderboard_context()
    {
        var variable = await BuildStackAsync();

        var snapshot = await Wired.GetBoxSnapshotAsync((RoomObjectId)LEADERBOARD, Ct);
        var context = snapshot!
            .ContextSnapshots.OfType<WiredVariableLeaderboardContextSnapshot>()
            .Single();

        context.ContextType.Should().Be(WiredContextType.VariableLeaderboard);
        context.OverlayInstanceId.Should().Be(0, "not published yet");
        (context.TileX, context.TileY).Should().Be((3, 3));
        context.TotalCandidateCount.Should().Be(1);
        context
            .Choices.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new WiredVariableLeaderboardChoiceSnapshot
                {
                    VariableId = variable.GetVarSnapshot().VariableId.ToString(),
                    TechnicalName = "total_coins",
                    Persistent = true,
                    HasValue = true,
                    ApplicableLevelUpCount = 0,
                }
            );

        var packet = PacketHarness.Encode(
            new WiredFurniAddonEventMessageComposer { WiredData = snapshot }
        );
        var bytes = HexOf(packet);

        // Section 8: overlay 0, tile 3,3, 1 candidate, 1 choice (id, "total_coins", true, true, 0).
        bytes
            .Should()
            .Contain("00000008" + "00000000" + "00000003" + "00000003" + "00000001" + "00000001");
    }

    [Fact]
    public async Task The_official_save_is_answered_with_the_official_snapshot_and_chunk()
    {
        // Official client capture 2026-10-13 (WIN63-202610131318): the add-on saved on the stack of
        // the "hp" variable (id 303135) with these params, nobody holding a value yet.
        var variable = await BuildStackAsync("hp");
        var variableId = variable.GetVarSnapshot().VariableId.ToString();

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                LEADERBOARD,
                intParams: [0, 2, 0, 1, -1, -1, 0, 0, 0, 0, 10, 4, 0, 0, 3, 0, 0, 0, 0],
                stringParam: "HP	Test Descript	Username	Value" + new string('	', 13),
                variableIds: NoVariables()
            )
        )
            .Should()
            .BeTrue();

        _room.Enter(1, 1, 1);

        var official = (string hex) => hex.Replace("0006333033313335", HexString(variableId));
        var snapshot = (await SubscribeAsync(RoomOpen(), player: 1)).Single();

        HexOf(snapshot)
            .Should()
            .Be(
                official(
                    "00000001000000010000000100024850000D546573742044657363726970740000000A0000000400000000000000000000000000000002000000000008557365726E616D650000000300000001000556616C75650000000000000001000633303331333500024870000633303331333500000000"
                )
            );

        var chunk = (
            await SubscribeAsync(
                Hex(official("000000010000000100063330333133350000000100000000")),
                player: 1
            )
        ).Single();

        HexOf(chunk)
            .Should()
            .Be(official("00000001000000010006333033313335000000000000000000000000"));

        var editor = await Wired.GetBoxSnapshotAsync((RoomObjectId)LEADERBOARD, Ct);

        editor!
            .ContextSnapshots.OfType<WiredVariableLeaderboardContextSnapshot>()
            .Single()
            .OverlayInstanceId.Should()
            .Be(1, "the editor of a published add-on names its overlay");
    }

    // Official client capture 2026-10-14 (WIN63-202610131318, leaderboard on the "hp" stack): the
    // drag + Ready save, then Enable filtering + Compare with the literal 0, then a re-save with 5
    // and once more unchanged.
    private static readonly int[] OFFICIAL_DRAG =
    [
        0,
        2,
        0,
        1,
        -1,
        -1,
        0,
        0,
        0,
        0,
        11,
        8,
        -4,
        10,
        3,
        0,
        0,
        0,
        0,
    ];
    private static readonly int[] OFFICIAL_FILTER =
    [
        0,
        2,
        0,
        1,
        -1,
        -1,
        1,
        1,
        0,
        0,
        11,
        8,
        -4,
        10,
        3,
        0,
        0,
        0,
        0,
    ];

    [Fact]
    public async Task The_official_saves_push_a_snapshot_only_when_the_leaderboard_changed()
    {
        var variable = await BuildStackAsync("hp");
        var variableId = variable.GetVarSnapshot().VariableId.ToString();
        string[] filterIds = [variableId, .. Enumerable.Repeat("0", 11)];
        var now = 20_000L;

        _room.Enter(1, 1, 1);
        await SubscribeAsync(RoomOpen(), player: 1);

        async Task<int> SaveAsync(int[] ints, string[] ids, string text)
        {
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    LEADERBOARD,
                    intParams: ints,
                    stringParam: text,
                    variableIds: ids
                )
            )
                .Should()
                .BeTrue();

            var before = SnapshotsSentTo(101);
            await _room.Harness.Room.VariableLeaderboardSystem.OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(3, 3)],
                },
                Ct
            );
            now += 2_000;
            await _room.Harness.Room.VariableLeaderboardSystem.ProcessAsync(now, Ct);

            return SnapshotsSentTo(101) - before;
        }

        (
            await SaveAsync(
                OFFICIAL_DRAG,
                NoVariables(),
                "Lab\t\tUsername\tValue" + new string('\t', 13)
            )
        )
            .Should()
            .Be(1, "a published leaderboard is pushed to its viewers");
        (
            await SaveAsync(
                OFFICIAL_FILTER,
                filterIds,
                "Lab\t\tUsername\tValue\t\t\t0" + new string('\t', 10)
            )
        )
            .Should()
            .Be(1, "turning the filter on changed it");
        (
            await SaveAsync(
                OFFICIAL_FILTER,
                filterIds,
                "Lab\t\tUsername\tValue\t\t\t5" + new string('\t', 10)
            )
        )
            .Should()
            .Be(1, "the literal changed");
        (
            await SaveAsync(
                OFFICIAL_FILTER,
                filterIds,
                "Lab\t\tUsername\tValue\t\t\t5" + new string('\t', 10)
            )
        )
            .Should()
            .Be(0, "an unchanged re-save pushes nothing");
    }

    [Fact]
    public async Task A_save_with_backslash_t_text_instead_of_tabs_is_an_invalid_configuration()
    {
        // Official client capture 2026-10-14: answered WiredValidationError invalid_configuration.
        await BuildStackAsync("hp");

        var result = await _room.SaveWithResultAsync<UpdateAddonMessage>(
            LEADERBOARD,
            intParams: OFFICIAL_DRAG,
            stringParam: @"Lab\t\tUsername\tValue",
            variableIds: NoVariables()
        );

        result.IsSaved.Should().BeFalse();
        result.ErrorKey.Should().Be(WiredAddonVariableLeaderboard.ERROR_INVALID);
    }

    [Theory]
    [InlineData(WiredAvailabilityType.UserActive, false)]
    [InlineData(WiredAvailabilityType.Persistent, true)]
    [InlineData(WiredAvailabilityType.Shared, true)]
    public async Task Only_a_permanent_user_variable_counts_as_persisted_for_the_editor(
        WiredAvailabilityType availability,
        bool persistent
    )
    {
        // Official 2026-10-14: the red "no persisted user variable" warning went once "hp" was made
        // Permanent (UpdateVariable [10, 1]).
        var variable = await BuildStackAsync("hp");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                VARIABLE,
                intParams: [(int)availability, 1],
                stringParam: "hp"
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);

        var snapshot = await Wired.GetBoxSnapshotAsync((RoomObjectId)LEADERBOARD, Ct);

        snapshot!
            .ContextSnapshots.OfType<WiredVariableLeaderboardContextSnapshot>()
            .Single()
            .Choices.Single()
            .Persistent.Should()
            .Be(persistent);
    }

    private int SnapshotsSentTo(long playerId) =>
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Method == "SendComposerAsync" && x.Key is long key && key == playerId
            )
            .SelectMany(x => x.Args.OfType<VariableLeaderboardSnapshotMessageComposer>())
            .Count();

    private static string HexString(string text) =>
        Convert.ToHexString([
            (byte)(text.Length >> 8),
            (byte)text.Length,
            .. System.Text.Encoding.UTF8.GetBytes(text),
        ]);

    private async Task<string> BuildPublishedAsync(int[]? ints = null)
    {
        var variable = await BuildStackAsync();
        var variableId = variable.GetVarSnapshot().VariableId.ToString();

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                LEADERBOARD,
                intParams: ints ?? Ints(),
                stringParam: Strings(title: "Richest"),
                variableIds: NoVariables()
            )
        )
            .Should()
            .BeTrue();

        foreach (var (index, value) in new[] { (1, 5), (2, 20), (3, 10) })
        {
            _room.Enter(index, index, 0);
            (
                await variable.GiveValueAsync(
                    new WiredVariableKey(
                        variable.GetVarSnapshot().VariableId,
                        WiredVariableTargetType.User,
                        index
                    ),
                    new WiredVariableValue(value)
                )
            )
                .Should()
                .BeTrue();
        }

        return variableId;
    }

    /// <summary>A persisted user variable with a value, "total_coins", and a leaderboard on its tile.</summary>
    private async Task<WiredVariableUser> BuildStackAsync(string name = "total_coins")
    {
        var variable = _room.AddBox<WiredVariableUser>(VARIABLE, 3, 3, "wf_var_user");
        var leaderboard = _room.AddBox<WiredAddonVariableLeaderboard>(
            LEADERBOARD,
            3,
            3,
            "wf_xtra_var_leaderboard"
        );

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                VARIABLE,
                intParams: [(int)WiredAvailabilityType.Persistent, 1],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);
        await leaderboard.LoadWiredAsync(Ct);
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(3, 3)],
            },
            Ct
        );
        await Wired.ProcessWiredAsync(10_000, dormant: false, Ct);

        return variable;
    }

    private async Task<int> RevisionAsync()
    {
        var reply = (await SubscribeAsync(RoomOpen(), player: 1)).Single();

        reply.PopInt();
        reply.PopInt();

        return reply.PopInt();
    }

    /// <summary>Client bytes through the revision's parser, then the room, as the handler does.</summary>
    private async Task<List<Turbo.Primitives.Packets.ClientPacket>> SubscribeAsync(
        byte[] payload,
        int player
    )
    {
        var message = (VariableLeaderboardReplaceSubscriptionMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("VariableLeaderboardReplaceSubscriptionMessageEvent"),
                payload
            );
        var replies = await _room.Harness.Room.ReplaceVariableLeaderboardSubscriptionAsync(
            ActionContext.CreateForPlayer(100 + player, 1),
            message.OverlayInstanceId,
            message.QueryRevision,
            message.SelectedVariableId,
            message.ChunkIndices,
            Ct
        );

        return [.. replies.Select(PacketHarness.Encode)];
    }

    private static byte[] RoomOpen() => Hex("000000000000000000013000000000");

    private static byte[] Subscription(int overlay, int revision, string variableId, int chunk) =>
        PacketHarness.Payload(w =>
            w.Int(overlay).Int(revision).String(variableId).Int(1).Int(chunk)
        );

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static string HexOf(Turbo.Primitives.Packets.ClientPacket packet)
    {
        var bytes = new byte[packet.Remaining];

        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = packet.PopByte();

        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// The 19 ints the editor writes: highest value, Username + Value (widths XL, small), no
    /// filter, no display names, tile 3,4 offset -5 altitude 2, page size 15.
    /// </summary>
    private static int[] Ints() => [0, 2, 0, 1, -1, -1, 0, 0, 0, 0, 3, 4, -5, 2, 3, 0, 0, 0, 1];

    private static string[] NoVariables() => [.. Enumerable.Repeat("0", 12)];

    private static string Strings(string title) =>
        string.Join(
            '\t',
            new[] { title, "Who has the most coins", "Username", "Coins", "", "", "" }.Concat(
                Enumerable.Repeat("", 10)
            )
        );
}
