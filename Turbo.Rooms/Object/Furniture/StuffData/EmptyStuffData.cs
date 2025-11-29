using Turbo.Primitives.Rooms.Object.Furniture.StuffData;

namespace Turbo.Rooms.Object.Furniture.StuffData;

internal sealed class EmptyStuffData : StuffDataBase, IEmptyStuffData
{
    public override string GetLegacyString() => string.Empty;
}
