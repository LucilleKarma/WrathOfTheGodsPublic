using Microsoft.Xna.Framework;
using NoxusBoss.Content.Tiles.SolynRopes;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class RegisterSolynRopePacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        int startX = (int)context[0];
        int startY = (int)context[1];
        int endX = (int)context[2];
        int endY = (int)context[3];
        byte dropsItem = (byte)context[4];
        Vector2 start = new Vector2(startX, startY);
        Vector2 end = new Vector2(endX, endY);

        packet.WriteVector2(start);
        packet.WriteVector2(end);
        packet.Write(dropsItem);
    }

    public override void Read(BinaryReader reader)
    {
        Vector2 start = reader.ReadVector2();
        Vector2 end = reader.ReadVector2();
        byte dropsItem = reader.ReadByte();

        // This automatically handles the case in which a server and client firing both try to create a rope, as the system will reject
        // any "new" ropes that have the exact same start and end position as an existing rope.
        SolynRopeSystem.Register(new SolynRopeData(start.ToPoint(), end.ToPoint())
        {
            DropsItem = dropsItem != 0
        });
    }
}
