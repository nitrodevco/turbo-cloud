namespace Turbo.Primitives.Pets;

/// <summary>Riding as the client sees it: the rider wears this effect while mounted.</summary>
public static class PetRiding
{
    public const int RIDER_EFFECT_ID = 77;

    /// <summary><c>PetInfo.accessRights</c> value meaning anyone may ride.</summary>
    public const int ACCESS_ANYONE = 1;
    public const int ACCESS_OWNER = 0;
}
