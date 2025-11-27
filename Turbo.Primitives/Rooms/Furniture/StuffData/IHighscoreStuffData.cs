using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Furniture.StuffData;

public interface IHighscoreStuffData : IStuffData
{
    public int ScoreType { get; }
    public int ClearType { get; }
    public Dictionary<int, List<string>> HighscoreData { get; set; }
}
