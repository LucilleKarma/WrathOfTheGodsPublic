using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class TeleportNPCPacket : Packet
{
    public override bool ResendFromServer => false;

    public override void Write(ModPacket packet, params object[] context)
    {
        int npcIndex = (int)context[0];
        NPC npc = Main.npc[npcIndex];

        packet.Write(npcIndex);
        packet.WriteVector2(npc.position);
        packet.WriteVector2(npc.velocity);
    }

    public override void Read(BinaryReader reader)
    {
        int npcIndex = reader.ReadInt32();
        NPC npc = Main.npc[npcIndex];

        npc.position = reader.ReadVector2();
        npc.velocity = reader.ReadVector2();
        npc.netUpdate = true;
    }
}
