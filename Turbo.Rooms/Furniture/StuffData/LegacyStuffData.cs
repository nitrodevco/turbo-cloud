using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

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
