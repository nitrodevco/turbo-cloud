using System.Collections.Immutable;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

/// <summary>A rentable bot in a room: an avatar its owner configures through bot skills.</summary>
public interface IRoomBot : IRoomAvatar<IRoomBot, IRoomBotLogic, IRoomBotContext>
{
    new IRoomBotLogic Logic { get; }
    public int BotId { get; }
    public PlayerId OwnerId { get; }
    public string OwnerName { get; }
    public AvatarGenderType Gender { get; }
    public ImmutableArray<BotSkillType> Skills { get; }
    public bool FreeRoam { get; }
    public string ChatText { get; }
    public ImmutableArray<string> ChatLines { get; }
    public bool AutoChat { get; }
    public int ChatDelaySeconds { get; }
    public bool MixSentences { get; }
    public long NextWalkAtMs { get; set; }
    public long NextChatAtMs { get; set; }
    public int NextChatLineIndex { get; set; }

    /// <summary>The avatar a "follow" order attached the bot to, or -1.</summary>
    public RoomObjectId FollowObjectId { get; set; }

    /// <summary>The furni a "move" order sent the bot to, or -1.</summary>
    public RoomObjectId TargetItemId { get; set; }

    public void SetName(string name);
    public void SetMotto(string motto);
    public void SetFigure(string figure, AvatarGenderType gender);
    public void SetFreeRoam(bool freeRoam);
    public void SetChatter(string text, bool autoChat, int delaySeconds, bool mixSentences);
    public BotSnapshot GetBotSnapshot();
}
