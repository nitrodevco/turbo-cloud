using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class HighscoreStuffData : StuffDataBase, IHighscoreStuffData
{
    [JsonIgnore]
    public override StuffDataType StuffType => StuffDataType.HighscoreKey;

    public string Data { get; set; } = DEFAULT_STATE;
    public int ScoreType { get; set; } = -1;
    public int ClearType { get; set; } = -1;
    public List<HighscoreEntry> Entries { get; set; } = [];
    public long PeriodStartedAt { get; set; }

    public override string GetLegacyString() => Data;

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

        Data = state;

        MarkDirty();
    }

    public void SetTypes(int scoreType, int clearType)
    {
        if (ScoreType == scoreType && ClearType == clearType)
            return;

        ScoreType = scoreType;
        ClearType = clearType;

        MarkDirty();
    }

    public void SetEntries(List<HighscoreEntry> entries, long periodStartedAt)
    {
        Entries = entries;
        PeriodStartedAt = periodStartedAt;

        MarkDirty();
    }

    protected override StuffDataSnapshot BuildSnapshot() =>
        new HighscoreStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
            Data = GetLegacyString(),
            ScoreType = ScoreType,
            ClearType = ClearType,
            Entries = ToSnapshots(Entries),
        };

    internal static ImmutableArray<HighscoreEntrySnapshot> ToSnapshots(
        IEnumerable<HighscoreEntry> entries
    ) =>
        [
            .. entries.Select(entry => new HighscoreEntrySnapshot
            {
                Score = entry.Score,
                Users = [.. entry.Users],
            }),
        ];
}
