using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Campaign;

/// <summary>
/// The answer to opening a door (AS3 CampaignCalendarDoorOpenedMessageParser): whether it opened,
/// and what it gave - a product code, a picture under <c>image.library.url</c> and a furni class
/// name whose icon is drawn when there is no picture.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CampaignCalendarDoorOpenedMessageComposer : IComposer
{
    public static readonly CampaignCalendarDoorOpenedMessageComposer NotOpened = new()
    {
        DoorOpened = false,
        ProductName = string.Empty,
        CustomImage = string.Empty,
        FurnitureClassName = string.Empty,
    };

    [Id(0)]
    public required bool DoorOpened { get; init; }

    [Id(1)]
    public required string ProductName { get; init; }

    [Id(2)]
    public required string CustomImage { get; init; }

    [Id(3)]
    public required string FurnitureClassName { get; init; }
}
