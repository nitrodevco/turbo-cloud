using System.Collections.Generic;
using Turbo.Database.Entities.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata;

/// <summary>
/// The rows of the history (<c>gamedata_change_sets</c>, <c>gamedata_changes</c>), made one way:
/// a summary and a label cut to their columns. What a change's <c>before</c> and <c>after</c>
/// hold is the kind's own record, which the history reads back to roll it back.
/// </summary>
internal static class GamedataChangeSets
{
    /// <param name="player">The staff member who made it; null for one the server made on its own.</param>
    public static GamedataChangeSetEntity Create(
        GamedataChangeKind kind,
        string summary,
        PlayerId? player,
        List<GamedataChangeEntity> changes
    ) =>
        new()
        {
            Kind = kind,
            Summary = GamedataStrings.Truncate(summary, GamedataChangeSetEntity.SUMMARY_MAX_LENGTH),
            PlayerEntityId = player?.Value,
            Changes = changes,
        };

    /// <param name="recordId">The row changed; 0 for one not saved yet, set when it is.</param>
    public static GamedataChangeEntity Change(
        GamedataRecordType type,
        string label,
        string? before,
        string? after,
        int recordId = 0
    ) =>
        new()
        {
            RecordType = type,
            RecordId = recordId,
            Label = GamedataStrings.Truncate(label, GamedataChangeEntity.LABEL_MAX_LENGTH),
            Before = before,
            After = after,
        };
}
