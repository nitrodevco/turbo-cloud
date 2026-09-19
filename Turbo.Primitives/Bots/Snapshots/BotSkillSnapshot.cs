using Orleans;
using Turbo.Primitives.Bots.Enums;

namespace Turbo.Primitives.Bots.Snapshots;

/// <summary>A skill entry of <c>BotSkillListUpdate</c>; only link-style skills carry data.</summary>
[GenerateSerializer, Immutable]
public sealed record BotSkillSnapshot
{
    [Id(0)]
    public required BotSkillType Skill { get; init; }

    [Id(1)]
    public required string Data { get; init; }
}
