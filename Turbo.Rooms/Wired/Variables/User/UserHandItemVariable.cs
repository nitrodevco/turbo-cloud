using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>What the avatar is holding, by hand item id. Zero is an empty hand.</summary>
public sealed class UserHandItemVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomAvatar>(roomGrain)
{
    protected override string VariableName => "@handitem";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 120;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.CanCreateAndDelete
        | WiredVariableFlags.HasTextConnector;

    private IEnumerable<int> TextIds =>
        WiredTextConnectors.UpTo(_roomGrain._wiredConfig.MaxHandItemId);

    protected internal override string TextPrefix =>
        WiredTextConnectors.Prefix(WiredTextConnectors.HAND_ITEM_KEY);

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        WiredTextConnectors.ForIds(Texts, WiredTextConnectors.HAND_ITEM_KEY, TextIds);

    /// <summary>
    /// Writing it puts that hand item in the avatar's hand, or empties it with zero. It goes
    /// through the avatar module so the item still expires on its timer as one handed over by
    /// a player does.
    /// </summary>
    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (
            !CanBind(key)
            || !TryGetAvatarForKey(key, out var avatar)
            || value.Value < 0
            || value.Value > _roomGrain._wiredConfig.MaxHandItemId
        )
            return false;

        await AvatarModule.SetHandItemAsync(avatar, value.ClampToInt(), CancellationToken.None);

        return true;
    }

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(avatar.HandItemId);
}
