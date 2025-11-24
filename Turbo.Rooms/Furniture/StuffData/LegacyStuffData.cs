using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class LegacyStuffData : StuffDataBase, ILegacyStuffData
{
    public string Data { get; private set; } = "0";

    public override string GetLegacyString() => Data;

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "0";

        Data = state;
    }
}
