using NoxusBoss.Content.NPCs.Friendly;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class PlayerTalkToSolynPacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write((int)context[0]);
        packet.Write((int)context[1]);
    }

    public override void Read(BinaryReader reader)
    {
        Solyn solyn = Main.npc[reader.ReadInt32()].As<Solyn>();
        solyn.TalkingTo = reader.ReadInt32();
        if (solyn.TalkingTo != -1)
            solyn.TimeSinceLastTalk = 1;
    }
}
