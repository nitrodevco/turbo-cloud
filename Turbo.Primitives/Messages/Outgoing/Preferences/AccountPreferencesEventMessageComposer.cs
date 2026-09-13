using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Preferences;

[GenerateSerializer, Immutable]
public sealed record AccountPreferencesEventMessageComposer : IComposer
{
    [Id(0)]
    public required int GenericVolume { get; init; }

    [Id(1)]
    public required int FurniVolume { get; init; }

    [Id(2)]
    public required int TraxVolume { get; init; }

    [Id(3)]
    public required bool FreeFlowChatDisabled { get; init; }

    [Id(4)]
    public required bool RoomInvitesIgnored { get; init; }

    [Id(5)]
    public required bool RoomCameraFollowDisabled { get; init; }

    [Id(6)]
    public required UIFlags UIFlags { get; init; }

    [Id(7)]
    public required int PreferedChatStyle { get; init; }

    [Id(8)]
    public required bool WiredMenuButton { get; init; }

    [Id(9)]
    public required bool WiredInspectButton { get; init; }

    [Id(10)]
    public required bool PlayTestMode { get; init; }

    [Id(11)]
    public required int VariableSyntaxMode { get; init; }

    [Id(12)]
    public required bool WiredWhisperDisabled { get; init; }

    [Id(13)]
    public required bool ShowAllNotifications { get; init; }

    [Id(14)]
    public required string WiredUIStyle { get; init; }

    [Id(15)]
    public required ChatSizeType ChatSizePreference { get; init; }

    [Id(16)]
    public required ChatModeType ChatMode { get; init; }

    [Id(17)]
    public required ChatBubbleWidthType ChatBubbleWidth { get; init; }

    [Id(18)]
    public required ChatScrollSpeedType ChatScrollSpeed { get; init; }

    [Id(19)]
    public required int OnlineIndicatorPreference { get; init; }
}
