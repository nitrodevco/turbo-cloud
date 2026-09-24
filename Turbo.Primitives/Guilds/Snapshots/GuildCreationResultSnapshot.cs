using Orleans;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// Whether a group was made, and if not, why. The reason is an enum rather than a bare int so
/// the handler cannot invent one the hotel has no text for; see
/// <see cref="GuildCreationFailureType"/> for the one reason that has no text at all.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildCreationResultSnapshot
{
    [Id(0)]
    public required GuildId GuildId { get; init; }

    [Id(1)]
    public required RoomId RoomId { get; init; }

    [Id(2)]
    public GuildCreationFailureType? Failure { get; init; }

    public bool Succeeded => Failure is null;

    public static GuildCreationResultSnapshot Success(GuildId guildId, RoomId roomId) =>
        new() { GuildId = guildId, RoomId = roomId };

    public static GuildCreationResultSnapshot Failed(GuildCreationFailureType failure) =>
        new()
        {
            GuildId = GuildId.Invalid,
            RoomId = -1,
            Failure = failure,
        };
}
