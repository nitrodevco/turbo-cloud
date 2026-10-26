using System;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Games;

/// <summary>
/// Sulake's room game furni, known by their classnames as their server classes are in Sulake's
/// furni data (github 80O/FurniData): <c>BattleBallTeamGateFurniture</c> (<c>bb_gate_*</c>),
/// <c>IceStormTeamGateFurniture</c> (<c>es_gate_*</c>), <c>TeamScoreCounterFurniture</c>
/// (<c>bb_score_*</c> and <c>es_score_*</c>, one class for both games) and
/// <c>BattleBallFloorFurniture</c> (<c>bb_patch1</c>) and <c>BattleBallPuckFurniture</c> (<c>bb_puck</c>). The colour is the classname's last letter;
/// the team numbers in that data's <c>customparams</c> agree (<c>bb_gate_r</c> "1,33",
/// <c>_g</c> "2,34", <c>_b</c> "3,35", <c>_y</c> "4,36"; <c>bb_score_r</c> "1" to <c>_y</c> "4").
/// </summary>
public static class GameFurniture
{
    /// <summary>The logic of a game furni's classname, or null for any other furni.</summary>
    public static string? LogicOf(string className)
    {
        if (className == FurnitureBattleBanzaiTileLogic.CLASSNAME)
            return FurnitureBattleBanzaiTileLogic.LOGIC_NAME;

        if (className == FurnitureBattleBanzaiPuckLogic.CLASSNAME)
            return FurnitureBattleBanzaiPuckLogic.LOGIC_NAME;

        if (TryParseGate(className, out _, out _))
            return FurnitureTeamGateLogic.LOGIC_NAME;

        if (TryParseScore(className, out _))
            return FurnitureTeamScoreLogic.LOGIC_NAME;

        return null;
    }

    /// <summary>A team gate's team and the kind of team it puts players in.</summary>
    public static bool TryParseGate(string className, out GameTeamType team, out WiredTeamType type)
    {
        type = className.StartsWith("bb_gate_", StringComparison.Ordinal)
            ? WiredTeamType.BattleBanzai
            : WiredTeamType.Freeze;

        team = GameTeamType.None;

        return (
                className.StartsWith("bb_gate_", StringComparison.Ordinal)
                || className.StartsWith("es_gate_", StringComparison.Ordinal)
            ) && TryParseColour(className["xx_gate_".Length..], out team);
    }

    /// <summary>A team scoreboard's team.</summary>
    public static bool TryParseScore(string className, out GameTeamType team)
    {
        team = GameTeamType.None;

        return (
                className.StartsWith("bb_score_", StringComparison.Ordinal)
                || className.StartsWith("es_score_", StringComparison.Ordinal)
            ) && TryParseColour(className["xx_score_".Length..], out team);
    }

    private static bool TryParseColour(string colour, out GameTeamType team)
    {
        team = colour switch
        {
            "r" => GameTeamType.Red,
            "g" => GameTeamType.Green,
            "b" => GameTeamType.Blue,
            "y" => GameTeamType.Yellow,
            _ => GameTeamType.None,
        };

        return team != GameTeamType.None;
    }
}
