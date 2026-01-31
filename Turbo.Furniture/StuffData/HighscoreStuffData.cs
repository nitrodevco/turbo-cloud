using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
    public Dictionary<int, List<string>> HighscoreData { get; set; } = [];

    public override string GetLegacyString() => Data;

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "0";

        Data = state;

        MarkDirty();
    }

    public int GetScoreType() => ScoreType;

    public void SetScoreType(int scoreType)
    {
        ScoreType = scoreType;

        MarkDirty();
    }

    public int GetClearType() => ClearType;

    public void SetClearType(int clearType)
    {
        ClearType = clearType;

        MarkDirty();
    }

    public void SetScore(int score, string name)
    {
        if (!HighscoreData.TryGetValue(score, out List<string>? value))
        {
            value = [name];
            HighscoreData[score] = value;

            MarkDirty();

            return;
        }

        if (!value.Contains(name))
        {
            value.Add(name);

            MarkDirty();
        }
    }

    protected override StuffDataSnapshot BuildSnapshot() =>
        new HighscoreStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
            Data = GetLegacyString(),
            ScoreType = GetScoreType(),
            ClearType = GetClearType(),
            Scores = HighscoreData.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToImmutableArray()
            ),
        };
}
