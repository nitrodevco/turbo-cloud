using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class EmptyStuffData : StuffDataBase, IEmptyStuffData
{
    public override string GetLegacyString() => string.Empty;
}
