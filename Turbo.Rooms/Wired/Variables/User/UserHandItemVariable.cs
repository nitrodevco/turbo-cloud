using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// What the avatar is holding, by hand item id. An avatar with an empty hand does not hold the
/// variable at all: the client lists it with "can create/delete" and without "is always
/// available" (Flash variable properties, <c>@handitem</c>), so Give Variable puts something in
/// a hand, Remove Variable empties it, and "has variable" asks whether the hand is full. It
/// used to answer 0 for an empty hand, which put every avatar in the room on its holder list.
/// </summary>
public sealed class UserHandItemVariable(RoomGrain roomGrain) : UserVariable<IRoomAvatar>(roomGrain)
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

    private IEnumerable<int> TextIds => WiredTextConnectors.UpTo(WiredConfig.MaxHandItemId);

    protected internal override string TextPrefix =>
        WiredTextConnectors.Prefix(WiredTextConnectors.HAND_ITEM_KEY);

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        WiredTextConnectors.ForIds(Texts, WiredTextConnectors.HAND_ITEM_KEY, TextIds);

    /// <summary>
    /// Writing it changes what a full hand holds, or empties it with zero. An empty hand holds
    /// no variable to change; Give Variable is what fills one.
    /// </summary>
    public override Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    ) =>
        CanBind(key) && TryGetAvatarForKey(key, out var avatar) && avatar.HandItemId != 0
            ? PutInHandAsync(avatar, value)
            : Task.FromResult(false);

    /// <summary>
    /// Habbo sends <c>@handitem</c> with canCreateAndDelete on (hhus, WIN63-202610121709), so Give
    /// Variable and Remove Variable offer it: giving it puts the given hand item in an empty hand,
    /// or in any hand when the box overrides an existing value.
    /// </summary>
    public override Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    ) =>
        CanBind(key)
        && TryGetAvatarForKey(key, out var avatar)
        && (avatar.HandItemId == 0 || replace)
            ? PutInHandAsync(avatar, value)
            : Task.FromResult(false);

    /// <summary>Removing it empties the hand.</summary>
    public override bool RemoveValue(WiredVariableKey key)
    {
        if (!CanBind(key) || !TryGetAvatarForKey(key, out var avatar) || avatar.HandItemId == 0)
            return false;

        AvatarModule
            .SetHandItemAsync(avatar, 0, CancellationToken.None)
            .LogAndForget(Logger, "empty a hand from wired in room {RoomId}", _roomGrain.RoomId);

        return true;
    }

    /// <summary>
    /// Through the avatar module, so the item still expires on its timer as one handed over by a
    /// player does.
    /// </summary>
    private async Task<bool> PutInHandAsync(IRoomAvatar avatar, WiredVariableValue value)
    {
        if (value.Value < 0 || value.Value > WiredConfig.MaxHandItemId)
            return false;

        await AvatarModule.SetHandItemAsync(avatar, value.ClampToInt(), CancellationToken.None);

        return true;
    }

    protected override bool TryGetValueForAvatar(IRoomAvatar avatar, out WiredVariableValue value)
    {
        value = WiredVariableValue.Parse(avatar.HandItemId);

        return avatar.HandItemId != 0;
    }
}
