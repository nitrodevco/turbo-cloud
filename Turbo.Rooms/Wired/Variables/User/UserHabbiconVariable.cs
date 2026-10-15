using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// <c>@habbicon</c>: Habbo describes it as a writable value with creation and update times that
/// can be given and removed, and hides it from the editor (invisible; official client capture of
/// a production room's variable definitions, 2026-10-11). Only the description is built: what
/// holding or writing it does is not observable while Habbo hides it, so no avatar holds it.
/// </summary>
public sealed class UserHabbiconVariable(RoomGrain roomGrain) : UserVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@habbicon";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 190;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.CanCreateAndDelete
        | WiredVariableFlags.IsInvisible
        | WiredVariableFlags.CanReadCreationTime
        | WiredVariableFlags.CanReadLastUpdateTime
        | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        WiredHabbiconTextConnectors.Create();

    protected override bool TryGetValueForAvatar(IRoomPlayer avatar, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return false;
    }
}
