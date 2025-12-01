using System.Collections.Generic;

namespace Turbo.Primitives.Furniture.StuffData;

public interface IStringStuffData : IStuffData
{
    public List<string> Data { get; }
}
