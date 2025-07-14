using Microsoft.Xna.Framework;

using NoxusBoss.Content.NPCs.Friendly;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class BattleSolynTeleportEffectPacket : Packet
{
    public override void Read(BinaryReader reader)
    {
        var from = reader.ReadVector2();
        var to = reader.ReadVector2();

        BattleSolyn.PlayTeleportEffect(from, to);
    }

    public override void Write(ModPacket packet, params object[] context)
    {
        packet.WriteVector2((Vector2)context[0]);
        packet.WriteVector2((Vector2)context[1]);
    }
}
