using System.Collections.Generic;

namespace Turbo.Primitives.Furniture.StuffData;

public interface IMapStuffData : IStuffData
{
    public Dictionary<string, string> Data { get; }
}
