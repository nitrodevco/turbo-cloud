using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Availability;

[GenerateSerializer, Immutable]
public sealed record InfoHotelClosedMessageComposer : IComposer
{
    [Id(0)]
    public required int OpenHour { get; init; }

    [Id(1)]
    public required int OpenMinute { get; init; }

    /// <summary>Whether the closing threw the player out, which changes the client's wording.</summary>
    [Id(2)]
    public required bool UserThrownOutAtClose { get; init; }
}
