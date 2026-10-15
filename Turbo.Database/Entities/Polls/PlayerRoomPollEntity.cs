using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Rooms.Polls;

namespace Turbo.Database.Entities.Polls;

/// <summary>
/// Where a player is with a poll: started, turned down or finished. A poll is offered again only
/// while there is no row, or the player started it and never finished.
/// </summary>
[Table("player_room_polls")]
[Index(nameof(PlayerEntityId), nameof(PollId), IsUnique = true)]
public class PlayerRoomPollEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("poll_id")]
    public required int PollId { get; set; }

    [Column("state")]
    public required RoomPollState State { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(PollId))]
    public RoomPollEntity? Poll { get; set; }
}
