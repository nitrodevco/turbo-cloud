using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// A kind of sanction as the client's sanction info reads it (<c>readSanctionType</c>): its name
/// (<c>ALERT</c>, <c>MUTE</c>, <c>BAN_PERMANENT</c>, ...), its length in hours and a third
/// number the client never reads. <see cref="NONE"/> is the empty one sent for "no next sanction".
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SanctionTypeSnapshot
{
    public static readonly SanctionTypeSnapshot NONE = new()
    {
        Name = string.Empty,
        LengthHours = 0,
        Unknown = 0,
    };

    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required int LengthHours { get; init; }

    [Id(2)]
    public required int Unknown { get; init; }
}
