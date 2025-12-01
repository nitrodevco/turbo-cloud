using System.Collections.Generic;

namespace Turbo.Primitives.Furniture.StuffData;

public interface INumberStuffData : IStuffData
{
    public List<int> Data { get; }
}
