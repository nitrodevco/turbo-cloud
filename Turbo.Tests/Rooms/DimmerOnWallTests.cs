using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Features;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Wall;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Every moodlight Habbo has (roomdimmer, dimmer_swtch, dimmer_buttn, dimmer_fuse*) is a wall
/// item: furnidata lists them under <c>wallitemtypes</c>, category <c>dimmer</c>. The dimmer logic
/// took a floor item's context, so a wall item with it got no logic at all and could not be
/// placed (tester report 2026-10-15: "now i also cant place a mood switch").
/// </summary>
public sealed class DimmerOnWallTests
{
    private const int DIMMER = 51;
    private const int OWNER = 1;

    private readonly LiveRoomHarness _room = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void A_moodlight_on_the_wall_gets_the_dimmer_logic()
    {
        var dimmer = Dimmer();

        var ensured = () => EnsureLogic(dimmer);

        ensured.Should().NotThrow().Which.Should().Be(true);
        dimmer.Logic.GetUsagePolicy().Should().Be(FurnitureUsageType.Controller);
    }

    /// <summary>
    /// Switching a moodlight on reaches the room as the whole wall item (ItemUpdate), as Habbo
    /// sends a used wall item (official client capture 2026-10-13: a wall item used twice came
    /// back as ItemUpdate with data "0" then "1"). The client's dimmer logic lights the room from
    /// that data update only; ItemDataUpdate feeds a post-it's text alone
    /// (<c>RoomEngine.as:4018</c>, <c>FurnitureRoomDimmerLogic.as:95</c>), so the switch did
    /// nothing (tester report 2026-10-16: "room dimmer doesnt work").
    /// </summary>
    [Fact]
    public async Task Switching_a_moodlight_on_tells_the_room_the_whole_wall_item()
    {
        var dimmer = Dimmer();
        EnsureLogic(dimmer);
        _room.State.ItemsById.Add((RoomObjectId)DIMMER, dimmer);

        (
            await _room.Room.InteractWithItemAsync(
                ActionContext.CreateForPlayer(OWNER, 1),
                DIMMER,
                new ToggleDimmerInteraction(),
                Ct
            )
        )
            .Should()
            .BeTrue();

        var sent = _room.Fakes.SentToRoom<IComposer>().ToList();

        sent.OfType<ItemDataUpdateMessageComposer>().Should().BeEmpty();
        sent.OfType<ItemUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.WallItem.StuffData.Should()
            .BeOfType<LegacyStuffSnapshot>()
            .Which.Data.Should()
            .Be("2,1,1,#000000,255");
    }

    /// <summary>
    /// A saved preset is read back from the item's extra data, which stores it camel-cased. It was
    /// read case-sensitively, so every read fell back to the defaults: switching the moodlight off
    /// wrote the default black preset into its data and the dialog reopened on defaults (user
    /// report 2026-10-17: blue, full brightness, background only, turned off, reopened - "the
    /// preset's data was gone").
    /// </summary>
    [Fact]
    public async Task A_saved_preset_survives_switching_the_moodlight_off()
    {
        var dimmer = Dimmer();
        EnsureLogic(dimmer);
        _room.State.ItemsById.Add((RoomObjectId)DIMMER, dimmer);
        var ctx = ActionContext.CreateForPlayer(OWNER, 1);

        (
            await _room.Room.InteractWithItemAsync(
                ctx,
                DIMMER,
                new SaveDimmerPresetInteraction
                {
                    PresetId = 2,
                    EffectType = 2,
                    Color = "#0053F7",
                    Brightness = 255,
                    Apply = true,
                    KeepFurnitureLights = false,
                },
                Ct
            )
        )
            .Should()
            .BeTrue();
        (await _room.Room.InteractWithItemAsync(ctx, DIMMER, new ToggleDimmerInteraction(), Ct))
            .Should()
            .BeTrue();

        _room
            .Fakes.SentToRoom<ItemUpdateMessageComposer>()
            .Select(u => ((LegacyStuffSnapshot)u.WallItem.StuffData).Data)
            .Should()
            .Equal("2,2,2,#0053F7,255", "1,2,2,#0053F7,255");
    }

    /// <summary>
    /// Build 116's moodlight editor has "keep furniture lights unchanged" for each preset. The
    /// client sends it where older ones wrote false, and reads it back as a sixth part of the
    /// furni's data (<c>local1[5]?.toLowerCase() === "true"</c> in its dimmer logic) and as a
    /// boolean a preset in the preset list. A preset without it keeps the five parts it had.
    /// </summary>
    [Fact]
    public async Task A_preset_that_keeps_furni_lights_says_so_in_the_data_and_the_preset_list()
    {
        var dimmer = Placed();
        var ctx = ActionContext.CreateForPlayer(OWNER, 1);

        (await SaveAsync(ctx, presetId: 2, keepFurnitureLights: true)).Should().BeTrue();
        (await _room.Room.InteractWithItemAsync(ctx, DIMMER, new ToggleDimmerInteraction(), Ct))
            .Should()
            .BeTrue();
        (await SaveAsync(ctx, presetId: 3, keepFurnitureLights: false)).Should().BeTrue();

        _room
            .Fakes.SentToRoom<ItemUpdateMessageComposer>()
            .Select(u => ((LegacyStuffSnapshot)u.WallItem.StuffData).Data)
            .Should()
            .Equal("2,2,2,#0053F7,255,true", "1,2,2,#0053F7,255,true", "2,3,2,#0053F7,255");

        (
            await _room.Room.InteractWithItemAsync(
                ctx,
                DIMMER,
                new RequestDimmerPresetsInteraction(),
                Ct
            )
        )
            .Should()
            .BeTrue();

        _room
            .Fakes.SentToPlayer<RoomDimmerPresetsMessageComposer>(OWNER)
            .Should()
            .ContainSingle()
            .Which.Presets.Select(x => x.KeepFurnitureLights)
            .Should()
            .Equal(false, true, false);
        dimmer.ExtraData.GetJsonString().Should().Contain("\"keepFurnitureLights\":true");
    }

    /// <summary>
    /// Presets stored before the flag existed have no such property, and data written then has
    /// five parts. Both still read, the flag off.
    /// </summary>
    [Fact]
    public async Task A_preset_stored_before_the_flag_reads_with_it_off()
    {
        Placed(
            """
            {
              "stuff": { "Data": "2,2,2,#0053F7,200" },
              "dimmer": [{ "id": 2, "type": 2, "color": "#0053F7", "brightness": 200 }]
            }
            """
        );
        var ctx = ActionContext.CreateForPlayer(OWNER, 1);

        await _room.Room.InteractWithItemAsync(
            ctx,
            DIMMER,
            new RequestDimmerPresetsInteraction(),
            Ct
        );
        await _room.Room.InteractWithItemAsync(ctx, DIMMER, new ToggleDimmerInteraction(), Ct);

        var sent = _room
            .Fakes.SentToPlayer<RoomDimmerPresetsMessageComposer>(OWNER)
            .Should()
            .ContainSingle()
            .Subject;

        sent.SelectedPresetId.Should().Be(2);
        sent.Presets[1]
            .Should()
            .Be(
                new DimmerPresetSnapshot
                {
                    Id = 2,
                    Type = 2,
                    Color = "#0053F7",
                    Brightness = 200,
                    KeepFurnitureLights = false,
                }
            );
        _room
            .Fakes.SentToRoom<ItemUpdateMessageComposer>()
            .Select(u => ((LegacyStuffSnapshot)u.WallItem.StuffData).Data)
            .Should()
            .Equal("1,2,2,#0053F7,200");
    }

    /// <summary>
    /// Switching a moodlight whose data already has the sixth part reads its state and preset
    /// from the first two, as before.
    /// </summary>
    [Fact]
    public async Task Data_with_the_sixth_part_still_reads_its_state_and_preset()
    {
        Placed(
            """
            {
              "stuff": { "Data": "2,3,1,#FF0000,128,true" },
              "dimmer": [
                { "id": 3, "type": 1, "color": "#FF0000", "brightness": 128, "keepFurnitureLights": true }
              ]
            }
            """
        );

        await _room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            DIMMER,
            new ToggleDimmerInteraction(),
            Ct
        );

        _room
            .Fakes.SentToRoom<ItemUpdateMessageComposer>()
            .Select(u => ((LegacyStuffSnapshot)u.WallItem.StuffData).Data)
            .Should()
            .Equal("1,3,1,#FF0000,128,true");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_save_packet_carries_the_flag_where_older_clients_write_false(bool keep)
    {
        var message = (RoomDimmerSavePresetMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("RoomDimmerSavePresetMessageEvent"),
                PacketHarness.Payload(w =>
                    w.Int(2).Int(1).String("#0053F7").Int(200).Bool(true).Bool(keep).Int(DIMMER)
                )
            );

        message
            .Should()
            .Be(
                new RoomDimmerSavePresetMessage
                {
                    PresetId = 2,
                    EffectType = 1,
                    Color = "#0053F7",
                    Brightness = 200,
                    Apply = true,
                    KeepFurnitureLights = keep,
                    ObjectId = DIMMER,
                }
            );
    }

    /// <summary>
    /// An older client reads four fields a preset; one with <c>room_dimmer_lights</c> reads a
    /// boolean after them (build 116's <c>RoomDimmerPresetsMessageParser</c>). Sending either
    /// the other's layout shifts everything after the first preset.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_preset_list_is_written_in_the_layout_the_client_reads(bool feature)
    {
        var composer = new RoomDimmerPresetsMessageComposer
        {
            ItemId = DIMMER,
            IsOn = true,
            SelectedPresetId = 2,
            Presets =
            [
                new DimmerPresetSnapshot
                {
                    Id = 1,
                    Type = 1,
                    Color = "#000000",
                    Brightness = 255,
                },
                new DimmerPresetSnapshot
                {
                    Id = 2,
                    Type = 2,
                    Color = "#0053F7",
                    Brightness = 200,
                    KeepFurnitureLights = true,
                },
            ],
        };
        var serializer = feature
            ? PacketHarness.Revision.FeatureSerializers[ClientFeatures.ROOM_DIMMER_LIGHTS][
                typeof(RoomDimmerPresetsMessageComposer)
            ]
            : PacketHarness.Revision.Serializers[typeof(RoomDimmerPresetsMessageComposer)];

        using var written = serializer.Serialize(composer);
        var packet = new ClientPacket(serializer.Header, written.ToArray().AsMemory(6));

        serializer.Header.Should().Be(PacketHarness.Outgoing("RoomDimmerPresetsMessageComposer"));
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(2);

        foreach (var preset in composer.Presets)
        {
            packet.PopInt().Should().Be(preset.Id);
            packet.PopInt().Should().Be(preset.Type);
            packet.PopString().Should().Be(preset.Color);
            packet.PopInt().Should().Be(preset.Brightness);

            if (feature)
                packet.PopBoolean().Should().Be(preset.KeepFurnitureLights);
        }

        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(DIMMER);
        packet.End.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, "2,1,1,#000000,255")]
    [InlineData(true, "2,1,1,#000000,255,true")]
    public void The_data_gains_its_sixth_part_only_for_a_preset_that_keeps_furni_lights(
        bool keep,
        string expected
    )
    {
        var data = DimmerStates.Compose(
            DimmerStates.ON,
            new DimmerPresetSnapshot
            {
                Id = 1,
                Type = DimmerStates.EFFECT_ROOM,
                Color = DimmerStates.DEFAULT_COLOR,
                Brightness = DimmerStates.MAX_BRIGHTNESS,
                KeepFurnitureLights = keep,
            }
        );

        data.Should().Be(expected);
        DimmerStates.KeepsFurnitureLights(data.Split(DimmerStates.SEPARATOR)).Should().Be(keep);
    }

    private Task<bool> SaveAsync(ActionContext ctx, int presetId, bool keepFurnitureLights) =>
        _room.Room.InteractWithItemAsync(
            ctx,
            DIMMER,
            new SaveDimmerPresetInteraction
            {
                PresetId = presetId,
                EffectType = 2,
                Color = "#0053F7",
                Brightness = 255,
                Apply = true,
                KeepFurnitureLights = keepFurnitureLights,
            },
            Ct
        );

    private RoomWallItem Placed(string? extraData = null)
    {
        var dimmer = Dimmer(extraData);

        EnsureLogic(dimmer);
        _room.State.ItemsById.Add((RoomObjectId)DIMMER, dimmer);

        return dimmer;
    }

    private bool EnsureLogic(RoomWallItem dimmer) =>
        _room.Module<RoomObjectModule>().EnsureLogic(dimmer);

    private static RoomWallItem Dimmer(string? extraData = null)
    {
        var dimmer = new RoomWallItem
        {
            ObjectId = DIMMER,
            OwnerId = OWNER,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 4027,
                SpriteId = 4027,
                Name = "roomdimmer",
                ProductType = ProductType.Wall,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "dimmer",
                TotalStates = 0,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.Zero,
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = false,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Controller,
                ExtraData = null,
            },
        };
        dimmer.SetExtraData(extraData);

        return dimmer;
    }
}
