using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Tests.Support;

/// <summary>
/// The way a composer left, as the fakes record it: a handler's <c>ctx.SendComposerAsync</c> to
/// the session it handles, <c>SendComposerToPlayerAsync</c> to a player's presence (one composer
/// or a batch), or the room's stream (<c>SendComposerToRoomAsync</c> and friends, one
/// <see cref="RoomOutboundSnapshot"/> per publish).
/// </summary>
[Flags]
public enum SendRoute
{
    Session = 1,
    Player = 2,
    Room = 4,
    Direct = Session | Player,
    Any = Direct | Room,
}

/// <summary>
/// What the code under test sent, read from a <see cref="Fakes"/> log in the order it was sent.
/// <c>TrySendComposerAsync</c> is not a send here: the fake presence answers it <c>false</c>
/// (not attached), so a test of that route reads the log itself.
/// </summary>
public static class SentComposers
{
    /// <summary>Every composer sent by the given routes (all of them by default), in order.</summary>
    public static IEnumerable<IComposer> Sent(this Fakes fakes, SendRoute routes = SendRoute.Any) =>
        fakes.Log.Calls.SelectMany(call => Composers(call, routes));

    /// <summary>Every composer of type <typeparamref name="T"/> sent by the given routes, in order.</summary>
    public static IEnumerable<T> Sent<T>(this Fakes fakes, SendRoute routes = SendRoute.Any) =>
        fakes.Sent(routes).OfType<T>();

    /// <summary>Every composer of type <typeparamref name="T"/> the room published to everyone in it, in order.</summary>
    public static IEnumerable<T> SentToRoom<T>(this Fakes fakes) => fakes.Sent<T>(SendRoute.Room);

    /// <summary>Each publish to the room's stream: its composers together, and whom it leaves out.</summary>
    public static IEnumerable<RoomOutboundSnapshot> RoomPublishes(this Fakes fakes) =>
        fakes.Log.Calls.SelectMany(call => call.Args).OfType<RoomOutboundSnapshot>();

    /// <summary>
    /// Every composer of type <typeparamref name="T"/> sent to one player's presence (one or a
    /// batch), in order; not what the room published to everyone.
    /// </summary>
    public static IEnumerable<T> SentToPlayer<T>(this Fakes fakes, long playerId) =>
        fakes
            .Log.Calls.Where(call =>
                call.Interface == typeof(IPlayerPresenceGrain) && IsPlayer(call.Key, playerId)
            )
            .SelectMany(call => Composers(call, SendRoute.Player))
            .OfType<T>();

    /// <summary>The avatars of each <see cref="UserUpdateMessageComposer"/> the room sent, one array per update.</summary>
    public static List<RoomAvatarSnapshot[]> AvatarUpdates(this Fakes fakes) =>
        [.. fakes.SentToRoom<UserUpdateMessageComposer>().Select(x => x.Avatars.ToArray())];

    private static IEnumerable<IComposer> Composers(FakeCall call, SendRoute routes)
    {
        if (call.Method == nameof(IPlayerPresenceGrain.SendComposerAsync))
        {
            var route =
                call.Interface == typeof(IPlayerPresenceGrain)
                    ? SendRoute.Player
                    : SendRoute.Session;

            return (routes & route) == 0 ? [] : Unwrap(call.Args.FirstOrDefault());
        }

        return (routes & SendRoute.Room) == 0
            ? []
            : call.Args.OfType<RoomOutboundSnapshot>().SelectMany(x => x.Composers);
    }

    private static IEnumerable<IComposer> Unwrap(object? sent) =>
        sent switch
        {
            IComposer one => [one],
            IEnumerable<IComposer> many => many,
            _ => [],
        };

    private static bool IsPlayer(object? key, long playerId) =>
        key switch
        {
            long n => n == playerId,
            int n => n == playerId,
            PlayerId p => p.Value == playerId,
            _ => false,
        };
}
