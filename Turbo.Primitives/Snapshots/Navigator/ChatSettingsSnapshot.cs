using Turbo.Contracts.Enums.Navigator.Chat;

namespace Turbo.Primitives.Snapshots.Navigator;

public sealed record ChatSettingsSnapshot(
    ChatModeType ChatMode,
    ChatBubbleWidthType BubbleWidth,
    ChatScrollSpeedType ScrollSpeed,
    int FullHearRange,
    ChatFloodSensitivityType FloodSensitivity
);
