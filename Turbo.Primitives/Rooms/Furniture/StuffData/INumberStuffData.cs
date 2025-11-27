using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Furniture.StuffData;

public interface INumberStuffData : IStuffData
{
    public List<int> Data { get; }
}
