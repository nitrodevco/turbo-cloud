using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The client knows a furni is temporary by its id alone (WIN63-202610131318
/// <c>com/sulake/habbo/utils/_-T1R.as:26</c> <c>isTempId</c>: 2147401728 .. 2147418111); only then
/// does its info stand draw the temporary colours and "Temporary Furni" with the wired icon where
/// the owner goes (<c>InfoStandFurniView.as:574</c>, <c>:597</c>; official screenshot 2026-10-14,
/// pt-BR "Móbi Temporário"). Ours numbered them -1, -2, ... so the info stand showed an ordinary
/// furni.
/// </summary>
public sealed class TemporaryFurniIdBandTests
{
    private const int CLIENT_TEMP_MIN = 2147401728;
    private const int CLIENT_TEMP_MAX = 2147418111;
    private const int LAMP = 20;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomFurniModule Furni => _room.Harness.Module<RoomFurniModule>();

    public TemporaryFurniIdBandTests()
    {
        _room.Harness.Fakes.Handlers["CreateFloorItem"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                OwnerId = (PlayerId)call.Args[1]!,
                OwnerName = string.Empty,
                Definition = (FurnitureDefinitionSnapshot)call.Args[2]!,
            };

            item.SetExtraData(null);

            return item;
        };
    }

    [Fact]
    public async Task Temporary_furni_take_ids_the_client_reads_as_temporary()
    {
        var definition = _room.AddFloorItem(LAMP, 0, 0).Definition;

        var first = await PlaceAsync(definition, 2, 2);
        var second = await PlaceAsync(definition, 3, 3);

        first.ObjectId.Value.Should().Be(CLIENT_TEMP_MIN);
        second.ObjectId.Value.Should().Be(CLIENT_TEMP_MIN + 1);
        new[] { first, second }
            .Should()
            .OnlyContain(x =>
                x.IsTemporary
                && !x.IsBuildersClub
                && x.ObjectId.Value >= CLIENT_TEMP_MIN
                && x.ObjectId.Value <= CLIENT_TEMP_MAX
            );
        _room.FloorItem(LAMP).IsTemporary.Should().BeFalse();
    }

    [Fact]
    public async Task A_removed_temporary_furni_frees_its_id()
    {
        var definition = _room.AddFloorItem(LAMP, 0, 0).Definition;
        var first = await PlaceAsync(definition, 2, 2);
        await PlaceAsync(definition, 3, 3);

        (await Furni.RemoveTemporaryItemAsync(ActionContext.CreateForSystem(1), first, Ct))
            .Should()
            .BeTrue();

        (await PlaceAsync(definition, 4, 4)).ObjectId.Value.Should().Be(CLIENT_TEMP_MIN);
    }

    private async Task<Turbo.Primitives.Rooms.Object.Furniture.Floor.IRoomFloorItem> PlaceAsync(
        FurnitureDefinitionSnapshot definition,
        int x,
        int y
    ) =>
        (
            await Furni.PlaceTemporaryFloorItemAsync(
                ActionContext.CreateForSystem(1),
                definition,
                (PlayerId)0,
                x,
                y,
                null,
                Rotation.North,
                Ct
            )
        )!;
}
