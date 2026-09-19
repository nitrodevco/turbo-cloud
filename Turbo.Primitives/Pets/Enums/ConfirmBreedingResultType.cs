namespace Turbo.Primitives.Pets.Enums;

/// <summary>Second field of <c>ConfirmBreedingResult</c>; anything but <see cref="Ok"/> is an alert.</summary>
public enum ConfirmBreedingResultType
{
    Ok = 0,
    NoNest = 1,
    PetsMissing = 2,
    InvalidName = 3,
}
