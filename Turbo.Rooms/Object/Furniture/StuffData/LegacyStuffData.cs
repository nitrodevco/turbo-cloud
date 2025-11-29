using Turbo.Primitives.Rooms.Object.Furniture.StuffData;

namespace Turbo.Rooms.Object.Furniture.StuffData;

internal sealed class LegacyStuffData : StuffDataBase, ILegacyStuffData
{
    public string Data { get; set; } = DEFAULT_STATE;

    public override string GetLegacyString() => Data;

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

        Data = state;
    }
}
