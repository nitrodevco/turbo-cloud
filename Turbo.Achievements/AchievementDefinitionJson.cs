using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Achievements;

/// <summary>
/// Reads stored and imported definitions. A definition written before <c>State</c> existed carries
/// <c>Enabled</c> and <c>Archived</c> flags instead; reading it without them would silently turn a
/// disabled or archived achievement into an enabled one, so they are mapped here.
/// </summary>
public static class AchievementDefinitionJson
{
    public static AchievementDefinition Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var definition =
            document.RootElement.Deserialize<AchievementDefinition>()
            ?? throw new InvalidOperationException("Empty achievement definition.");

        return Upgrade(definition, document.RootElement);
    }

    public static ImmutableArray<AchievementDefinition> ReadAll(string json)
    {
        using var document = JsonDocument.Parse(json);

        return
        [
            .. document
                .RootElement.EnumerateArray()
                .Select(x =>
                    Upgrade(
                        x.Deserialize<AchievementDefinition>()
                            ?? throw new InvalidOperationException("Empty achievement definition."),
                        x
                    )
                ),
        ];
    }

    private static AchievementDefinition Upgrade(AchievementDefinition definition, JsonElement raw)
    {
        if (
            raw.ValueKind != JsonValueKind.Object
            || raw.TryGetProperty(nameof(AchievementDefinition.State), out _)
        )
            return definition;
        var hasEnabled = raw.TryGetProperty("Enabled", out var enabled);
        var hasArchived = raw.TryGetProperty("Archived", out var archived);
        if (!hasEnabled && !hasArchived)
            return definition;
        var isArchived = hasArchived && archived.ValueKind == JsonValueKind.True;
        var isEnabled = !hasEnabled || enabled.ValueKind != JsonValueKind.False;

        return definition with
        {
            State =
                isArchived ? AchievementState.Archived
                : isEnabled ? AchievementState.Enabled
                : AchievementState.Disabled,
        };
    }
}
