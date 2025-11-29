using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object.Furniture.StuffData;

namespace Turbo.Rooms.Object.Furniture.StuffData;

internal sealed class HighscoreStuffData : StuffDataBase, IHighscoreStuffData
{
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
    }

    public int GetScoreType() => ScoreType;

    public void SetScoreType(int scoreType) => ScoreType = scoreType;

    public int GetClearType() => ClearType;

    public void SetClearType(int clearType) => ClearType = clearType;

    public void SetScore(int score, string name)
    {
        if (!HighscoreData.TryGetValue(score, out List<string>? value))
        {
            value = [name];
            HighscoreData[score] = value;

            return;
        }

        if (!value.Contains(name))
            value.Add(name);
    }
}
