using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Directory;

internal class Game2GameDirectoryStatusMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2GameDirectoryStatusMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2GameDirectoryStatusMessageMessageComposer message
    )
    {
        packet
            .WriteInteger(message.Status.Status)
            .WriteInteger(message.Status.BlockLength)
            .WriteInteger(message.Status.GamesPlayed)
            .WriteInteger(message.Status.FreeGamesLeft);
    }
}
