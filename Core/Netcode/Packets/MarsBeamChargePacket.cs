using NoxusBoss.Content.NPCs.Bosses.Draedon;

using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class MarsBeamChargePacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        int charge = MarsBody.Myself?.As<MarsBody>()?.SolynPlayerTeamAttackTimer ?? 0;
        packet.Write(charge);
    }

    public override void Read(BinaryReader reader)
    {
        int charge = reader.ReadInt32();
        if (MarsBody.Myself is not null)
        {
            MarsBody.Myself.As<MarsBody>().SolynPlayerTeamAttackTimer = charge;
        }
    }
}
