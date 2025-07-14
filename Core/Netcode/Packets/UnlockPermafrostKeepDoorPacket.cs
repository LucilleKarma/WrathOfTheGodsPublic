using NoxusBoss.Core.Graphics.SpecificEffectManagers;

using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class UnlockPermafrostKeepDoorPacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write((int)context[0]);
        packet.Write((int)context[1]);
    }

    public override void Read(BinaryReader reader)
    {
        var i = reader.ReadInt32();
        var j = reader.ReadInt32();
        PermafrostDoorUnlockSystem.Start(new(i, j));
    }
}
