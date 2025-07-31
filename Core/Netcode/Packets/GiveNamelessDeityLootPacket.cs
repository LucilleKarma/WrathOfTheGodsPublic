using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class GiveNamelessDeityLootPacket : Packet
{
    public override bool ResendFromServer => false;

    public override void Read(BinaryReader reader)
    {
        Player player = Main.player[reader.ReadInt32()];
        player.GetValueRef<bool>(NamelessDeityBoss.PlayerGiveLootFieldName).Value = true;
    }

    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write((int)context[0]);
    }
}
