using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.RoomSettings;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The owner's category and trade shortcut is confirmed like the settings dialog: Habbo answers
/// it with RoomSettingsSaved, then the room's RoomInfoUpdated, RoomVisualizationSettings and
/// RoomChatSettings (official client capture 2026-10-13, two saves, both in that order). The
/// navigator reloads its room list on RoomSettingsSaved (<c>NewIncomingMessages.onRoomSettingsSaved</c>).
/// Turbo sent only the room's part.
/// </summary>
public sealed class RoomCategoryAndTradeSavedTests
{
    private const int PLAYER = 101;
    private const int ROOM = 7;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IEnumerable<IComposer> Replies() =>
        _fakes.Log.Of("SendComposerAsync").SelectMany(call => call.Args.OfType<IComposer>());

    private Task SaveAsync(RoomSettingsSaveResultSnapshot result)
    {
        _fakes.Handlers["GetFlatCategoriesForPlayerAsync"] = _ =>
            Task.FromResult(ImmutableArray<NavigatorFlatCategorySnapshot>.Empty);
        _fakes.Handlers["UpdateCategoryAndTradeSettingsAsync"] = _ => Task.FromResult(result);
        _fakes.Handlers["RoomExistsAsync"] = _ => Task.FromResult(true);

        return new UpdateRoomCategoryAndTradeSettingsMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<INavigatorService>()
        )
            .HandleAsync(
                new UpdateRoomCategoryAndTradeSettingsMessage
                {
                    RoomId = ROOM,
                    CategoryId = 1,
                    TradeType = RoomTradeModeType.Everyone,
                },
                new MessageContext(_fakes.Create<ISessionContext>(), PLAYER, ROOM),
                Ct
            )
            .AsTask();
    }

    [Fact]
    public async Task A_saved_category_and_trade_change_is_confirmed_to_the_owner()
    {
        await SaveAsync(RoomSettingsSaveResultSnapshot.Success);

        Replies()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<RoomSettingsSavedEventMessageComposer>()
            .Which.RoomId.Value.Should()
            .Be(ROOM);
    }

    [Fact]
    public async Task A_refused_change_gets_the_error_and_no_confirmation()
    {
        await SaveAsync(RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.Invalid));

        Replies()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<RoomSettingsSaveErrorEventMessageComposer>();
    }
}
