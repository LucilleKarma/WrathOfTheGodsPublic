using Microsoft.Xna.Framework;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class SetSolynFlagPositionPacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        Point point = (Point)context[0];
        packet.Write(point.X);
        packet.Write(point.Y);
    }

    public override void Read(BinaryReader reader)
    {
        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        SolynCampsiteWorldGen.FlagPosition = new Point(x, y);
    }
}
