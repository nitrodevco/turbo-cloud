using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Navigator;
using Turbo.PacketHandlers.Room.Action;
using Turbo.PacketHandlers.RoomSettings;
using Turbo.Primitives;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room requests that name their room by id (settings, rights, bans, word filter, staff
/// pick) for a room id with no room behind it. Like the room info request (official client
/// capture 2026-10-18, 7 of 7 silent), they get no answer and wake no room grain. Before, each
/// woke one for the id, and its activation threw "The specified room could not be found".
/// </summary>
public sealed class MissingRoomRequestTests
{
    private const int PLAYER = 101;
    private const int MISSING = 131;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MissingRoomRequestTests()
    {
        _fakes.Handlers["RoomExistsAsync"] = _ => Task.FromResult(false);
    }

    public static TheoryData<Type> Handlers =>
        [
            typeof(GetRoomSettingsMessageHandler),
            typeof(SaveRoomSettingsMessageHandler),
            typeof(UpdateRoomCategoryAndTradeSettingsMessageHandler),
            typeof(GetFlatControllersMessageHandler),
            typeof(GetBannedUsersFromRoomMessageHandler),
            typeof(GetRaidProtectionSettingsMessageHandler),
            typeof(SaveRaidProtectionSettingsMessageHandler),
            typeof(GetCustomRoomFilterMessageHandler),
            typeof(UpdateRoomFilterMessageHandler),
            typeof(RemoveAllRightsMessageHandler),
            typeof(UnbanUserFromRoomMessageHandler),
            typeof(RemoveOwnRoomRightsRoomMessageHandler),
            typeof(ToggleStaffPickMessageHandler),
        ];

    [Theory]
    [MemberData(nameof(Handlers))]
    public async Task A_request_for_a_missing_room_gets_no_answer_and_wakes_no_room(
        Type handlerType
    )
    {
        var handler = Activator.CreateInstance(
            handlerType,
            [
                .. handlerType
                    .GetConstructors()
                    .Single()
                    .GetParameters()
                    .Select(p => _fakes.Create(p.ParameterType)),
            ]
        )!;
        var messageType = handlerType
            .GetInterfaces()
            .Single(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)
            )
            .GetGenericArguments()[0];
        var message = Activator.CreateInstance(messageType)!;

        messageType.GetProperty("RoomId")!.SetValue(message, (RoomId)MISSING);
        messageType.GetProperty("UserId")?.SetValue(message, PLAYER + 1);

        var result = (ValueTask)
            handlerType
                .GetMethod("HandleAsync")!
                .Invoke(
                    handler,
                    [message, new MessageContext(_fakes.Create<ISessionContext>(), PLAYER, -1), Ct]
                )!;
        await result;

        Sent().Should().BeEmpty();
        _fakes.Log.On<IRoomGrain>().Should().BeEmpty();
    }

    private IEnumerable<IComposer> Sent() =>
        _fakes
            .Log.Of("SendComposerAsync")
            .SelectMany(call => call.Args)
            .SelectMany(arg =>
                arg switch
                {
                    IComposer one => [one],
                    IEnumerable many => many.OfType<IComposer>(),
                    _ => [],
                }
            );
}
