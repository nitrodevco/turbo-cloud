using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

internal class StuffDataSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, StuffDataSnapshot item)
    {
        packet.WriteInteger(item.StuffBitmask);

        switch (item)
        {
            case EmptyStuffSnapshot empty:
                break;
            case HighscoreStuffSnapshot highscore:
                packet
                    .WriteString(highscore.Data)
                    .WriteInteger(highscore.ScoreType)
                    .WriteInteger(highscore.ClearType)
                    .WriteInteger(highscore.Scores.Count);

                foreach (var score in highscore.Scores)
                {
                    packet.WriteInteger(score.Key).WriteInteger(score.Value.Length);

                    foreach (var name in score.Value)
                        packet.WriteString(name);
                }
                break;
            case LegacyStuffSnapshot legacy:
                packet.WriteString(legacy.Data);
                break;
            case MapStuffSnapshot map:
                packet.WriteInteger(map.Data.Count);
                foreach (var (key, value) in map.Data)
                    packet.WriteString(key).WriteString(value);
                break;
            case NumberStuffSnapshot number:
                packet.WriteInteger(number.Data.Length);
                foreach (var value in number.Data)
                    packet.WriteInteger(value);
                break;
            case StringStuffSnapshot str:
                packet.WriteInteger(str.Data.Length);
                foreach (var value in str.Data)
                    packet.WriteString(value);
                break;
            case VoteStuffSnapshot vote:
                packet.WriteString(vote.Data).WriteInteger(vote.Result);
                break;
        }

        if ((item.StuffBitmask & (int)StuffDataFlags.Unique) != 0)
            packet.WriteInteger(item.UniqueNumber).WriteInteger(item.UniqueSeries);
    }
}
