using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Furniture.Data;

public sealed class StuffDataFactory
{
    private const int TYPE_MASK = 0xFF;
    private const int FLAGS_MASK = 0xFF00;

    public static IStuffData CreateStuffData(int typeAndFlags)
    {
        var type = (StuffDataTypeEnum)(typeAndFlags & TYPE_MASK);
        var flags = (StuffDataFlags)(typeAndFlags & FLAGS_MASK);

        IStuffData data = type switch
        {
            StuffDataTypeEnum.LegacyKey => new LegacyStuffData(),
            StuffDataTypeEnum.MapKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.StringKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.VoteKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.EmptyKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.NumberKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.HighscoreKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.CrackableKey => throw new System.NotImplementedException(),
            _ => throw new System.NotImplementedException(),
        };

        return data;
    }
}
