using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;

namespace Turbo.Database.Entities.WiredLink;

/// <summary>
/// The saved configuration of one placed WIRED Link: the network it is connected to (none when
/// it only receives its own room's sends) and whether it receives what its own room sends. Keyed
/// by room and furni id, as a Builders Club furni id is only unique within its room. Kept while
/// the room is unloaded; picking the Link up deletes it.
/// </summary>
[Table("wired_link_endpoints")]
[PrimaryKey(nameof(RoomEntityId), nameof(FurniId))]
[Index(nameof(PlayerEntityId))]
[Index(nameof(NetworkEntityId))]
public class WiredLinkEndpointEntity
{
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("furni_id")]
    public required int FurniId { get; set; }

    /// <summary>The Link's owner, who owns its network.</summary>
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("network_id")]
    public int? NetworkEntityId { get; set; }

    [Column("receive_local")]
    public bool ReceiveLocal { get; set; }

    [Column("builders_club")]
    public bool IsBuildersClub { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(NetworkEntityId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public WiredLinkNetworkEntity? NetworkEntity { get; set; }
}
