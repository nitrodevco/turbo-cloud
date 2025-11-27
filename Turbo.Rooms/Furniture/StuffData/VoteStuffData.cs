using System.Collections.Generic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class VoteStuffData : StuffDataBase, IVoteStuffData
{
    public string Data { get; set; } = "0";
    public int Result { get; set; } = 0;

    public override string GetLegacyString() => Data;

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "0";

        Data = state;
    }

    public int GetResult() => Result;

    public void SetResult(int result) => Result = result;
}
