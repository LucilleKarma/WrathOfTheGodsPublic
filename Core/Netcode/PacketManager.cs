using Microsoft.Xna.Framework;

using NoxusBoss.Core.Netcode.Packets;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.Netcode;

// NOTE -- This system or more or less equivalent to the one I wrote for Infernum a while ago.
public class PacketManager : ModSystem
{
    internal static Dictionary<string, Packet> RegisteredPackets = [];
    private const int VECTOR = 1;
    private const int POINT = 2;

    public override void OnModLoad()
    {
        RegisteredPackets = [];
        foreach (Type t in AssemblyManager.GetLoadableTypes(Mod.Code))
        {
            if (!t.IsSubclassOf(typeof(Packet)) || t.IsAbstract)
                continue;

            Packet packet = (Activator.CreateInstance(t) as Packet)!;
            RegisteredPackets[t.FullName!] = packet;
        }
    }

    internal static void PreparePacket(Packet packet, object[] context, short? sender = null)
    {
        // Don't try to send packets in singleplayer.
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        // Assume the sender is the current client if nothing else is supplied.
        sender ??= (short)Main.myPlayer;

        ModPacket wrapperPacket = ModContent.GetInstance<NoxusBoss>().GetPacket();

        // Write the identification header. This is necessary to ensure that on the receiving end the reader know how to interpret the packet.
        wrapperPacket.Write(packet.GetType().FullName!);

        // Write the sender and original context if the packet needs to be re-sent from the server.
        if (packet.ResendFromServer)
        {
            wrapperPacket.Write(sender.Value);

            // Send the context data.
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            TagCompound tagCompound = new TagCompound();
            for (int i = 0; i < context.Length; i++)
            {
                //Add type so we can deserialize vectors/points correct way
                tagCompound[$"{i}_type"] = context[i] switch
                {
                    Point => POINT,
                    Vector2 => VECTOR,
                    _ => 0
                };

                tagCompound[$"{i}"] = context[i];
            }

            TagIO.Write(tagCompound, writer);

            byte[] contextBytes = stream.ToArray();
            wrapperPacket.Write(context.Length);
            wrapperPacket.Write(contextBytes.Length);
            wrapperPacket.Write(contextBytes);
        }

        // Write the requested packet data.
        packet.Write(wrapperPacket, context);

        // Send the packet.
        wrapperPacket.Send(-1, sender.Value);
    }

    public static void SendPacket<T>(params object[] context) where T : Packet
    {
        // Verify that the packet is registered before trying to send it.
        string packetName = typeof(T).FullName!;
        if (Main.netMode == NetmodeID.SinglePlayer || !RegisteredPackets.TryGetValue(packetName, out Packet? packet))
            return;

        PreparePacket(packet, context);
    }

    public static void ReceivePacket(BinaryReader reader)
    {
        // Read the identification header to determine how the packet should be processed.
        string packetName = reader.ReadString();

        // If no valid packet could be found, get out of here.
        // There will inevitably be a reader underflow error caused by TML's packet policing, but there aren't any clear-cut solutions that
        // I know of that adequately addresses that problem, and from what I can tell it's never catastrophic when it happens.
        if (!RegisteredPackets.TryGetValue(packetName, out Packet? packet))
            return;

        // Determine who sent this packet if it needs to resend.
        short sender = -1;
        object[] context = [];
        if (packet.ResendFromServer)
        {
            sender = reader.ReadInt16();
            int contextLength = reader.ReadInt32();
            int contextByteCount = reader.ReadInt32();
            byte[] contextBytes = reader.ReadBytes(contextByteCount);
            using MemoryStream stream = new MemoryStream(contextBytes);
            using BinaryReader contextReader = new BinaryReader(stream);

            TagCompound tag = TagIO.Read(contextReader);
            context = new object[contextLength];
            for (int i = 0; i < contextLength; i++)
            {
                var value = tag.Get<object>($"{i}");
                var valueType = tag.Get<int>($"{i}_type");
                
                switch (valueType)
                {
                    case VECTOR:
                        {
                            var subTag = (TagCompound)value;
                            context[i] = new Vector2(subTag.Get<float>("x"), subTag.Get<float>("y"));
                        }
                        break;
                    case POINT:
                        {
                            var subTag = (TagCompound)value;
                            context[i] = new Point(subTag.Get<int>("x"), subTag.Get<int>("y"));
                        }
                        break;
                    default:
                        context[i] = value; 
                        break;
                }
            }
        }

        // Read off requested packet data.
        try
        {
            packet.Read(reader);
        }
        catch (Exception e)
        {
            ModContent.GetInstance<NoxusBoss>().Logger.Warn($"Error arose when attempting to process the '{packetName}' packet: {e}");
            throw;
        }

        // If this packet was received server-side and the packet needs to be re-sent, send it back to all the clients, with the
        // exception of the one that originally brought this packet to the server.
        if (Main.netMode == NetmodeID.Server && packet.ResendFromServer)
            PreparePacket(packet, context, sender);
    }
}
