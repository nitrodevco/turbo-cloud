using System;
using System.Text;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Object.Avatars.Pet;

/// <summary>
/// A pet standing in a room. Built from the persisted snapshot on placement or room activation;
/// the room mutates the live stats and rebuilds the snapshot when the pet is picked up or
/// flushed to the database.
/// </summary>
public sealed class RoomPetAvatar : RoomAvatar<IRoomPet, IRoomPetLogic, IRoomPetContext>, IRoomPet
{
    public override RoomObjectType AvatarType { get; } = RoomObjectType.Pet;

    public required int PetId { get; init; }
    public required PlayerId OwnerId { get; init; }
    public required string OwnerName { get; init; }
    public required RoomId RoomId { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public PetFigureSnapshot PetFigure { get; private set; } = default!;
    public int Level { get; private set; }
    public int Experience { get; private set; }
    public int Energy { get; private set; }
    public int Nutrition { get; private set; }
    public int Respect { get; private set; }
    public int RarityLevel { get; init; }
    public bool HasSaddle { get; private set; }
    public bool AnyoneCanRide { get; private set; }
    public bool HasBreedingPermission { get; private set; }
    public DateTime WateredAtUtc { get; private set; }
    public DateTime? HarvestedAtUtc { get; private set; }
    public RoomObjectId RiderObjectId { get; private set; } = -1;
    public RoomObjectId PendingRiderObjectId { get; set; } = -1;
    public string Posture { get; private set; } = PetPostures.STAND;
    public bool CanBreed { get; private set; }
    public bool CanHarvest { get; private set; }
    public bool CanRevive { get; private set; }

    public bool IsSilenced { get; set; }
    public bool IsFreeRoaming { get; set; } = true;
    public RoomObjectId FollowObjectId { get; set; } = -1;
    public int FollowOffset { get; set; }
    public RoomObjectId TargetItemId { get; set; } = -1;
    public long NextActionAtMs { get; set; }
    public long ActionExpiresAtMs { get; set; }
    public long NextEnergyDecayAtMs { get; set; }
    public long NextNutritionDecayAtMs { get; set; }

    public int TypeId => PetFigure.TypeId;
    public bool IsMonsterplant => PetTypes.IsMonsterplant(PetFigure.TypeId);
    public bool IsRiding => RiderObjectId > 0;

    public static RoomPetAvatar FromSnapshot(RoomObjectId objectId, PetSnapshot snapshot)
    {
        var pet = new RoomPetAvatar
        {
            ObjectId = objectId,
            PetId = snapshot.Id,
            OwnerId = snapshot.OwnerId,
            OwnerName = snapshot.OwnerName,
            RoomId = snapshot.RoomId ?? -1,
            CreatedAtUtc = snapshot.CreatedAtUtc,
            RarityLevel = snapshot.RarityLevel,
            PetFigure = snapshot.Figure,
            Level = snapshot.Level,
            Experience = snapshot.Experience,
            Energy = snapshot.Energy,
            Nutrition = snapshot.Nutrition,
            Respect = snapshot.Respect,
            HasSaddle = snapshot.HasSaddle,
            AnyoneCanRide = snapshot.AnyoneCanRide,
            HasBreedingPermission = snapshot.HasBreedingPermission,
            WateredAtUtc = snapshot.WateredAtUtc,
            HarvestedAtUtc = snapshot.HarvestedAtUtc,
        };

        pet.Name = snapshot.Name;
        pet.SetPosition(snapshot.X, snapshot.Y);
        pet.SetPositionZ(snapshot.Z);
        pet.SetRotation(snapshot.Rotation);

        return pet;
    }

    public void SetName(string name)
    {
        Name = name;

        MarkDirty();
    }

    public void SetPetFigure(PetFigureSnapshot figure)
    {
        PetFigure = figure;
        Figure = Primitives.Pets.PetFigure.ToFigureString(figure);

        MarkDirty();
    }

    public void SetLevel(int level)
    {
        Level = level;

        MarkDirty();
    }

    public void SetExperience(int experience) => Experience = Math.Max(0, experience);

    public void SetEnergy(int energy) => Energy = Math.Max(0, energy);

    public void SetNutrition(int nutrition) => Nutrition = Math.Max(0, nutrition);

    public void SetRespect(int respect) => Respect = Math.Max(0, respect);

    public void SetSaddle(bool hasSaddle)
    {
        HasSaddle = hasSaddle;

        MarkDirty();
    }

    public void SetAnyoneCanRide(bool anyoneCanRide) => AnyoneCanRide = anyoneCanRide;

    public void SetBreedingPermission(bool hasPermission)
    {
        HasBreedingPermission = hasPermission;

        MarkDirty();
    }

    public void SetRider(RoomObjectId riderObjectId)
    {
        RiderObjectId = riderObjectId;

        MarkDirty();
    }

    public void SetPosture(string posture)
    {
        Posture = posture;

        MarkDirty();
    }

    public void SetWateredAt(DateTime wateredAtUtc) => WateredAtUtc = wateredAtUtc;

    public void SetHarvestedAt(DateTime? harvestedAtUtc) => HarvestedAtUtc = harvestedAtUtc;

    /// <summary>The context-menu flags the room derives from stats and config.</summary>
    public void SetFlags(bool canBreed, bool canHarvest, bool canRevive)
    {
        if (CanBreed == canBreed && CanHarvest == canHarvest && CanRevive == canRevive)
            return;

        CanBreed = canBreed;
        CanHarvest = canHarvest;
        CanRevive = canRevive;

        MarkDirty();
    }

    public PetSnapshot GetPetSnapshot() =>
        new()
        {
            Id = PetId,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            RoomId = RoomId,
            Name = Name,
            Figure = PetFigure,
            Level = Level,
            Experience = Experience,
            Energy = Energy,
            Nutrition = Nutrition,
            Respect = Respect,
            RarityLevel = RarityLevel,
            HasSaddle = HasSaddle,
            AnyoneCanRide = AnyoneCanRide,
            HasBreedingPermission = HasBreedingPermission,
            X = X,
            Y = Y,
            Z = Z,
            Rotation = Rotation,
            CreatedAtUtc = CreatedAtUtc,
            WateredAtUtc = WateredAtUtc,
            HarvestedAtUtc = HarvestedAtUtc,
        };

    protected override RoomPetAvatarSnapshot BuildSnapshot()
    {
        var statusString = new StringBuilder("/");

        foreach (var (type, value) in Statuses)
            statusString.Append($"{type.ToLegacyString()} {value}/");

        return new()
        {
            AvatarType = AvatarType,
            WebId = PetId,
            Name = Name,
            Motto = Motto,
            Figure = Primitives.Pets.PetFigure.ToFigureString(PetFigure),
            ObjectId = ObjectId,
            X = X,
            Y = Y,
            Z = Z,
            BodyRotation = Rotation,
            HeadRotation = HeadRotation,
            JumpPower = JumpPower,
            Status = statusString.ToString(),
            DanceType = DanceType,
            EffectId = EffectId,
            SubType = PetFigure.TypeId,
            OwnerId = OwnerId,
            OwnerName = OwnerName,
            RarityLevel = RarityLevel,
            HasSaddle = HasSaddle,
            IsRiding = IsRiding,
            CanBreed = CanBreed,
            CanHarvest = CanHarvest,
            CanRevive = CanRevive,
            HasBreedingPermission = HasBreedingPermission,
            PetLevel = Level,
            PetPosture = Posture,
        };
    }
}
