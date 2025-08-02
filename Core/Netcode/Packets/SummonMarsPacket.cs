using NoxusBoss.Content.NPCs.Bosses.Draedon.Draedon;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class SummonMarsPacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {

    }

    public override void Read(BinaryReader reader)
    {
        int draedonIndex = NPC.FindFirstNPC(ModContent.NPCType<QuestDraedon>());
        if (draedonIndex == -1)
            return;

        NPC draedon = Main.npc[draedonIndex];
        draedon.As<QuestDraedon>().ChangeAIState(QuestDraedon.DraedonAIType.WaitForMarsToArrive);
    }
}
