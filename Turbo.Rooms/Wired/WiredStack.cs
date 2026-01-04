using System.Collections.Generic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Wired;

public sealed class WiredStack
{
    public required int StackId { get; init; }
    public List<FurnitureWiredTriggerLogic> Triggers { get; init; } = [];
    public List<FurnitureWiredSelectorLogic> Selectors { get; init; } = [];
    public List<FurnitureWiredConditionLogic> Conditions { get; init; } = [];
    public List<FurnitureWiredAddonLogic> Addons { get; init; } = [];
    public List<FurnitureWiredVariableLogic> Variables { get; init; } = [];
    public List<FurnitureWiredActionLogic> Effects { get; init; } = [];
}
