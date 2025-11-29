namespace Turbo.Primitives.Rooms.Object.Avatars;

public static class RoomAvatarStatusTypeExtensions
{
    public static string ToLegacyString(this RoomAvatarStatusType productType) =>
        productType switch
        {
            RoomAvatarStatusType.Move => "mv",
            RoomAvatarStatusType.Slide => "sld",
            RoomAvatarStatusType.Sit => "sit",
            RoomAvatarStatusType.Lay => "lay",
            RoomAvatarStatusType.FlatControl => "flatctrl",
            RoomAvatarStatusType.Sign => "sign",
            RoomAvatarStatusType.Gesture => "gst",
            RoomAvatarStatusType.Wave => "wav",
            RoomAvatarStatusType.Trading => "trd",
            RoomAvatarStatusType.Dip => "dip",
            RoomAvatarStatusType.Eat => "eat",
            RoomAvatarStatusType.Beg => "beg",
            RoomAvatarStatusType.Dead => "ded",
            RoomAvatarStatusType.Jump => "jmp",
            RoomAvatarStatusType.Play => "pla",
            RoomAvatarStatusType.Speak => "spk",
            RoomAvatarStatusType.Croak => "crk",
            RoomAvatarStatusType.Relax => "rlx",
            RoomAvatarStatusType.Wings => "wng",
            RoomAvatarStatusType.Flame => "flm",
            RoomAvatarStatusType.Rip => "rip",
            RoomAvatarStatusType.Grow => "grw",
            RoomAvatarStatusType.Grow1 => "grw1",
            RoomAvatarStatusType.Grow2 => "grw2",
            RoomAvatarStatusType.Grow3 => "grw3",
            RoomAvatarStatusType.Grow4 => "grw4",
            RoomAvatarStatusType.Grow5 => "grw5",
            RoomAvatarStatusType.Grow6 => "grw6",
            RoomAvatarStatusType.Grow7 => "grw7",
            RoomAvatarStatusType.LayIn => "lay-in",
            RoomAvatarStatusType.LayOut => "lay-out",
            RoomAvatarStatusType.Kick => "kck",
            RoomAvatarStatusType.WagTail => "wag",
            RoomAvatarStatusType.JumpIn => "jmp-in",
            RoomAvatarStatusType.JumpOut => "jmp-out",
            _ => throw new System.NotImplementedException(),
        };
}
