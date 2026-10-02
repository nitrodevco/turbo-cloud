using Turbo.Primitives.Commands;

namespace Turbo.Operations.Commands;

public sealed record AchievementAdminArguments(
    PlayerTarget Who,
    AchievementAdminAction Action,
    int AchievementId,
    long Progress,
    string OperationId,
    RestOfLine Reason
);
