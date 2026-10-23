using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Directory;

/// <summary>The client's <c>GameLobbyData</c> and <c>GameLobbyPlayerData</c>.</summary>
internal static class GameLobbyWriter
{
    public static void Write(IServerPacket packet, GameLobbySnapshot lobby)
    {
        packet
            .WriteInteger(lobby.GameId)
            .WriteString(lobby.LevelName)
            .WriteInteger(lobby.GameType)
            .WriteInteger(lobby.FieldType)
            .WriteInteger(lobby.NumberOfTeams)
            .WriteInteger(lobby.MaximumPlayers)
            .WriteString(lobby.OwningPlayerName)
            .WriteInteger(lobby.LevelEntryId)
            .WriteInteger(lobby.Players.Length);

        foreach (var player in lobby.Players)
            WritePlayer(packet, player);
    }

    public static void WritePlayer(IServerPacket packet, GameLobbyPlayerSnapshot player) =>
        packet
            .WriteInteger(player.PlayerId)
            .WriteString(player.Name)
            .WriteString(player.Figure)
            .WriteString(player.Gender)
            .WriteInteger(player.TeamId)
            .WriteInteger(player.SkillLevel)
            .WriteInteger(player.TotalScore)
            .WriteInteger(player.ScoreToNextLevel);
}
