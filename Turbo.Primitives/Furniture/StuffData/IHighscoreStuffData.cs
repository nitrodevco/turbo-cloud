using System.Collections.Generic;

namespace Turbo.Primitives.Furniture.StuffData;

public interface IHighscoreStuffData : IStuffData
{
    public int ScoreType { get; }
    public int ClearType { get; }

    /// <summary>The rows, in the order the board lists them.</summary>
    public List<HighscoreEntry> Entries { get; }

    /// <summary>When the board's current period began (unix seconds); 0 before its first score.</summary>
    public long PeriodStartedAt { get; }

    public void SetTypes(int scoreType, int clearType);

    /// <summary>Replaces the rows and the period they belong to.</summary>
    public void SetEntries(List<HighscoreEntry> entries, long periodStartedAt);
}

/// <summary>One row of a highscore board.</summary>
public sealed class HighscoreEntry
{
    public int Score { get; set; }
    public List<string> Users { get; set; } = [];
}
