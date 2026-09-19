using Turbo.Database.Entities.Bots;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Extensions;

/// <summary>Row to snapshot, shared by the inventory (unplaced bots) and rooms (placed bots).</summary>
public static class BotEntityExtensions
{
    public static BotSnapshot ToSnapshot(this BotEntity entity, string ownerName) =>
        new()
        {
            Id = entity.Id,
            OwnerId = PlayerId.Parse(entity.PlayerEntityId),
            OwnerName = ownerName,
            RoomId = entity.RoomEntityId is { } roomId ? new RoomId(roomId) : null,
            Name = entity.Name,
            Motto = entity.Motto,
            Figure = entity.Figure,
            Gender = entity.Gender,
            X = entity.X,
            Y = entity.Y,
            Z = entity.Z,
            Rotation = entity.Rotation == Rotation.None ? Rotation.North : entity.Rotation,
            FreeRoam = entity.FreeRoam,
            ChatText = entity.ChatText ?? string.Empty,
            AutoChat = entity.AutoChat,
            ChatDelaySeconds = entity.ChatDelaySeconds,
            MixSentences = entity.MixSentences,
            DanceType = entity.DanceType,
        };
}
