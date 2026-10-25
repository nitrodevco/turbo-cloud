using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Policies;

/// <summary>
/// A policy over a player entering a room the hotel lets them into (or to whose doorbell it lets
/// them ring): it allows, refuses with a reason the player is shown, or sends them to another room
/// instead. Bans, passwords, a full room and the doorbell itself are the hotel's own checks and
/// come first. A public class implementing it is found in a plugin's assembly as it loads and
/// taken away as it unloads; see <see cref="IGamePolicies"/> for the order several run in.
/// <para>
/// It runs off the room's turn and may ask the room anything. A room it redirects to is entered
/// as any other, this policy asked again, so it must not send a player back where they came from.
/// A policy that throws is logged and passed over: it allows.
/// </para>
/// </summary>
public interface IRoomEntryPolicy
{
    /// <summary>Lower runs first. 0 by default.</summary>
    int Order => 0;

    ValueTask<RoomEntryDecision> DecideAsync(RoomEntryRequest request, CancellationToken ct);
}

/// <summary>A player asking to enter a room; <paramref name="Doorbell"/> when they would ring at its door.</summary>
public sealed record RoomEntryRequest(PlayerId PlayerId, RoomId RoomId, bool Doorbell);

/// <summary>A policy's answer: allowed, refused with an optional reason, or sent to another room.</summary>
public sealed record RoomEntryDecision(
    bool Allowed,
    string? Reason = null,
    RoomId? RedirectRoomId = null
)
{
    public static readonly RoomEntryDecision Allow = new(true);

    public static RoomEntryDecision Refuse(string? reason = null) => new(false, reason);

    /// <summary>Into <paramref name="roomId"/> instead, with <paramref name="reason"/> shown first when given.</summary>
    public static RoomEntryDecision Redirect(RoomId roomId, string? reason = null) =>
        new(false, reason, roomId);
}
