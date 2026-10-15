using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Room;

namespace Turbo.Database.Entities.Polls;

/// <summary>
/// A poll the hotel offers to whoever enters <see cref="RoomEntityId"/> (Flash
/// <c>PollOfferDialog</c>): the offer shows <see cref="Headline"/> and <see cref="Summary"/>, the
/// questions follow once the player starts it (<c>PollContentDialog</c>), and
/// <see cref="EndMessage"/> thanks them at the end (<c>PollSession.showThanks</c>).
/// </summary>
[Table("room_polls")]
[Index(nameof(RoomEntityId), nameof(Enabled))]
public class RoomPollEntity : TurboEntity
{
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    /// <summary>Sent with the offer (<c>PollOfferParser</c>); the client does not show it.</summary>
    [Column("type")]
    [DefaultValue("")]
    public string Type { get; set; } = string.Empty;

    [Column("headline")]
    public required string Headline { get; set; }

    [Column("summary")]
    public required string Summary { get; set; }

    [Column("start_message")]
    [DefaultValue("")]
    public string StartMessage { get; set; } = string.Empty;

    [Column("end_message")]
    [DefaultValue("")]
    public string EndMessage { get; set; } = string.Empty;

    /// <summary>
    /// A score poll: the choice picked for a question decides which of its follow-up questions
    /// comes next (<c>PollContentDialog.getNextQuestion</c>).
    /// </summary>
    [Column("nps_poll")]
    [DefaultValue(false)]
    public bool NpsPoll { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }
}
