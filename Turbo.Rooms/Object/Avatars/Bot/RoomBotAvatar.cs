using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Bots;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Object.Avatars.Bot;

/// <summary>A rentable bot standing in a room; the client renders it exactly like a user.</summary>
public sealed class RoomBotAvatar : RoomAvatar<IRoomBot, IRoomBotLogic, IRoomBotContext>, IRoomBot
{
    public override RoomObjectType AvatarType { get; } = RoomObjectType.Bot;

    public required int BotId { get; init; }
    public required PlayerId OwnerId { get; init; }
    public required string OwnerName { get; init; }
    public required RoomId RoomId { get; init; }
    public required ImmutableArray<BotSkillType> Skills { get; init; }
    public AvatarGenderType Gender { get; private set; }
    public bool FreeRoam { get; private set; }
    public string ChatText { get; private set; } = string.Empty;
    public ImmutableArray<string> ChatLines { get; private set; } = [];
    public bool AutoChat { get; private set; }
    public int ChatDelaySeconds { get; private set; }
    public bool MixSentences { get; private set; }
    public long NextWalkAtMs { get; set; }
    public long NextChatAtMs { get; set; }
    public int NextChatLineIndex { get; set; }
    public RoomObjectId FollowObjectId { get; set; } = -1;
    public RoomObjectId TargetItemId { get; set; } = -1;

    public static RoomBotAvatar FromSnapshot(
        RoomObjectId objectId,
        BotSnapshot snapshot,
        ImmutableArray<BotSkillType> skills
    )
    {
        var bot = new RoomBotAvatar
        {
            ObjectId = objectId,
            BotId = snapshot.Id,
            OwnerId = snapshot.OwnerId,
            OwnerName = snapshot.OwnerName,
            RoomId = snapshot.RoomId ?? -1,
            Skills = skills,
            Gender = snapshot.Gender,
            FreeRoam = snapshot.FreeRoam,
        };

        bot.Name = snapshot.Name;
        bot.Motto = snapshot.Motto;
        bot.Figure = snapshot.Figure;
        bot.SetDance(snapshot.DanceType);
        bot.SetChatter(
            snapshot.ChatText,
            snapshot.AutoChat,
            snapshot.ChatDelaySeconds,
            snapshot.MixSentences
        );
        bot.SetPosition(snapshot.X, snapshot.Y);
        bot.SetPositionZ(snapshot.Z);
        bot.SetRotation(snapshot.Rotation);

        return bot;
    }

    public void SetName(string name)
    {
        Name = name;

        MarkDirty();
    }

    public void SetMotto(string motto)
    {
        Motto = motto;

        MarkDirty();
    }

    public void SetFigure(string figure, AvatarGenderType gender)
    {
        Figure = figure;
        Gender = gender;

        MarkDirty();
    }

    public void SetFreeRoam(bool freeRoam) => FreeRoam = freeRoam;

    public void SetChatter(string text, bool autoChat, int delaySeconds, bool mixSentences)
    {
        ChatText = text;
        ChatLines = BotChatLines.Split(text);
        AutoChat = autoChat;
        ChatDelaySeconds = delaySeconds;
        MixSentences = mixSentences;
        NextChatLineIndex = 0;
    }

    /// <summary>
    /// What the bot is, as it goes back to the inventory and the database. The effect is left out
    /// on purpose: it is room state something put on the bot, not part of how it was configured.
    /// </summary>
    public BotSnapshot GetBotSnapshot() =>
        new()
        {
            Id = BotId,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            RoomId = RoomId,
            Name = Name,
            Motto = Motto,
            Figure = Figure,
            Gender = Gender,
            X = X,
            Y = Y,
            Z = Z,
            Rotation = Rotation,
            FreeRoam = FreeRoam,
            ChatText = ChatText,
            AutoChat = AutoChat,
            ChatDelaySeconds = ChatDelaySeconds,
            MixSentences = MixSentences,
            DanceType = DanceType,
        };

    protected override RoomRentableBotAvatarSnapshot BuildSnapshot()
    {
        return new()
        {
            AvatarType = AvatarType,
            WebId = BotId,
            Name = Name,
            Motto = Motto,
            Figure = Figure,
            ObjectId = ObjectId,
            X = X,
            Y = Y,
            Z = Z,
            BodyRotation = Rotation,
            HeadRotation = HeadRotation,
            JumpPower = JumpPower,
            Status = BuildStatusString(),
            DanceType = DanceType,
            EffectId = EffectId,
            IsIdle = IsIdle,
            Gender = Gender,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            BotSkills = [.. Skills.Select(x => (short)x)],
        };
    }
}
