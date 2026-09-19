namespace Turbo.Primitives.Pets.Enums;

/// <summary>Status of <c>OpenPetPackageResult</c>; the client shows the info text for anything but <see cref="Ok"/>.</summary>
public enum PetNameValidationType
{
    Ok = 0,
    TooLong = 1,
    TooShort = 2,
    InvalidCharacters = 3,
    Forbidden = 4,
}
