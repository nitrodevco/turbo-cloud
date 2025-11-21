namespace Turbo.Primitives.Rooms.StuffData;

public sealed class LegacyStuffData : StuffDataBase
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
