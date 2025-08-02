using System.Reflection;

using NoxusBoss.Core.SolynEvents;

using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class EventStageUpdatedPacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write((string)context[0]);
        packet.Write((int)context[1]);
    }

    public override void Read(BinaryReader reader)
    {
        string? typeName = reader.ReadString();
        int stage = reader.ReadInt32();
        GetEvent(typeName).Stage = stage;
    }

    private static SolynEvent GetEvent(string typeName)
    {
        Type type = ModLoader.GetMod("NoxusBoss").Code.GetType(typeName)!;
        MethodInfo method = typeof(ModContent).GetMethod("GetInstance", BindingFlags.Static | BindingFlags.Public)!;
        method = method.MakeGenericMethod(type);

        return (SolynEvent)method.Invoke(null, [])!;
    }
}
