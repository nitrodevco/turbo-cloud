using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Pets;

/// <summary>
/// A line a pet may say when told to speak or on its own now and then. A line names its pet
/// type (as the client's <c>pet.configuration</c> numbers them), or none for a line every type
/// may say when its own type has no lines.
/// </summary>
[Table("pet_speech")]
[Index(nameof(TypeId))]
public class PetSpeechEntity : TurboEntity
{
    [Column("type_id")]
    public int? TypeId { get; set; }

    [Column("line")]
    [MaxLength(100)]
    public required string Line { get; set; }
}
