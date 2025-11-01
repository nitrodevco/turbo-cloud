using Orleans;
using Turbo.Contracts.Enums.Navigator.Chat;

namespace Turbo.Primitives.Snapshots.Navigator;

[GenerateSerializer, Immutable]
public sealed record ChatSettingsSnapshot
{
    [Id(0)]
    public required ChatModeType ChatMode { get; init; }

    [Id(1)]
    public required ChatBubbleWidthType BubbleWidth { get; init; }

    [Id(2)]
    public required ChatScrollSpeedType ScrollSpeed { get; init; }

    [Id(3)]
    public required int FullHearRange { get; init; }

    [Id(4)]
    public required ChatFloodSensitivityType FloodSensitivity { get; init; }
}
