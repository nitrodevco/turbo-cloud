namespace Turbo.Primitives.Pets.Enums;

/// <summary>
/// Pet commands as the client numbers them: a button in the training tool is labelled
/// <c>pet.command.&lt;id&gt;</c> and clicking it says "&lt;pet name&gt; &lt;label&gt;" in chat.
/// </summary>
public enum PetCommandType
{
    Free = 0,
    Sit = 1,
    LieDown = 2,
    ComeHere = 3,
    Beg = 4,
    PlayDead = 5,
    Stay = 6,
    Follow = 7,
    Stand = 8,
    Jump = 9,
    Speak = 10,
    Play = 11,
    Silent = 12,
    Nest = 13,
    Drink = 14,
    FollowLeft = 15,
    FollowRight = 16,
    PlayFootball = 17,

    /// <summary>The "breed" menu button of a nest-breedable pet is this command said in chat.</summary>
    Breed = 46,
}
