using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The effect the avatar is wearing, by id. Zero is none.</summary>
public sealed class UserEffectVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomAvatar>(roomGrain)
{
    protected override string VariableName => "@effect";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 110;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.HasTextConnector;

    private IEnumerable<int> TextIds =>
        WiredTextConnectors.UpTo(_roomGrain._wiredConfig.MaxEffectId);

    protected internal override string TextPrefix =>
        WiredTextConnectors.Prefix(WiredTextConnectors.EFFECT_KEY);

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        WiredTextConnectors.ForIds(Texts, WiredTextConnectors.EFFECT_KEY, TextIds);

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(avatar.EffectId);
}
