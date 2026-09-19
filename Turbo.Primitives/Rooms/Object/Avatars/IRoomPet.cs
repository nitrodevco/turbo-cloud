using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

/// <summary>
/// A pet in a room. The live stats belong to the room while the pet is placed; the snapshot
/// they are rebuilt into is what goes back to the inventory and the database.
/// </summary>
public interface IRoomPet : IRoomAvatar<IRoomPet, IRoomPetLogic, IRoomPetContext>
{
    new IRoomPetLogic Logic { get; }
    public int PetId { get; }
    public PlayerId OwnerId { get; }
    public string OwnerName { get; }
    public PetFigureSnapshot PetFigure { get; }
    public int TypeId { get; }
    public int Level { get; }
    public int Experience { get; }
    public int Energy { get; }
    public int Nutrition { get; }
    public int Respect { get; }
    public int RarityLevel { get; }
    public bool HasSaddle { get; }
    public bool AnyoneCanRide { get; }
    public bool HasBreedingPermission { get; }
    public bool IsMonsterplant { get; }
    public bool IsRiding { get; }

    /// <summary>The avatar riding this pet, or -1.</summary>
    public RoomObjectId RiderObjectId { get; }

    /// <summary>An avatar walking over to mount this pet, or -1.</summary>
    public RoomObjectId PendingRiderObjectId { get; set; }
    public string Posture { get; }
    public bool CanBreed { get; }
    public bool CanHarvest { get; }
    public bool CanRevive { get; }

    /// <summary>Whether the pet answers chat commands (the "silent" command mutes it).</summary>
    public bool IsSilenced { get; set; }

    /// <summary>Whether the pet wanders on its own between commands.</summary>
    public bool IsFreeRoaming { get; set; }

    /// <summary>The avatar the pet is following, or -1.</summary>
    public RoomObjectId FollowObjectId { get; set; }
    public int FollowOffset { get; set; }

    /// <summary>The item the pet is walking to (food, nest, breeding nest), or -1.</summary>
    public RoomObjectId TargetItemId { get; set; }
    public long NextActionAtMs { get; set; }
    public long ActionExpiresAtMs { get; set; }
    public long NextEnergyDecayAtMs { get; set; }
    public long NextNutritionDecayAtMs { get; set; }

    public void SetName(string name);
    public void SetPetFigure(PetFigureSnapshot figure);
    public void SetLevel(int level);
    public void SetExperience(int experience);
    public void SetEnergy(int energy);
    public void SetNutrition(int nutrition);
    public void SetRespect(int respect);
    public void SetSaddle(bool hasSaddle);
    public void SetAnyoneCanRide(bool anyoneCanRide);
    public void SetBreedingPermission(bool hasPermission);
    public void SetRider(RoomObjectId riderObjectId);
    public void SetPosture(string posture);
    public void SetWateredAt(System.DateTime wateredAtUtc);
    public void SetHarvestedAt(System.DateTime? harvestedAtUtc);
    public void SetFlags(bool canBreed, bool canHarvest, bool canRevive);
    public PetSnapshot GetPetSnapshot();
    public System.DateTime CreatedAtUtc { get; }
    public System.DateTime WateredAtUtc { get; }
    public System.DateTime? HarvestedAtUtc { get; }
}
