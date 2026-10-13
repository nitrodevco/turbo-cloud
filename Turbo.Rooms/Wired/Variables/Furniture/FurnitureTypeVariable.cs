using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

public sealed class FurnitureTypeVariable(RoomGrain roomGrain)
    : FurnitureValueVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@type";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 70;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        // Habbo's labels (hhus, WIN63-202610121709).
        new()
        {
            [WiredVariableValue.Parse((int)FurnitureOwnershipType.Normal)] = "Normal",
            [WiredVariableValue.Parse((int)FurnitureOwnershipType.BuildersClub)] = "BC",
            [WiredVariableValue.Parse((int)FurnitureOwnershipType.Temporary)] = "Temp",
        };

    protected override WiredVariableValue GetValueForItem(IRoomItem item) =>
        WiredVariableValue.Parse((int)item.Ownership);
}
