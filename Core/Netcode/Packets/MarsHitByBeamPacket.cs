using NoxusBoss.Content.NPCs.Bosses.Draedon;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class MarsHitByBeamPacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write((int)context[0]);
    }

    public override void Read(BinaryReader reader)
    {
        int identity = reader.ReadInt32();
        if (MarsBody.Myself is null)
            return;

        Projectile? projectile = Main.projectile.FirstOrDefault(x => x.identity == identity);
        if (projectile is null || projectile.owner == Main.myPlayer)
            return;

        MarsBody.Myself.As<MarsBody>().RegisterHitByTeamBeam(projectile);
    }
}
