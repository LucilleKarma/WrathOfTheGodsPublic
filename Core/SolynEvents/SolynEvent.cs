using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;

using SubworldLibrary;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.SolynEvents;

/// <summary>
/// Represents a progressable event involving Solyn, such as a quest.
/// </summary>
public abstract class SolynEvent : ModSystem
{
    /// <summary>
    /// The current stage of this event.
    /// </summary>
    public int Stage
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of stages that this event has.
    /// </summary>
    public abstract int TotalStages
    {
        get;
    }

    /// <summary>
    /// Solyn's ModNPC instance. Returns null if she's not present.
    /// </summary>
    public static Solyn? Solyn
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether this event has been started or not.
    /// </summary>
    public bool Started => Stage >= 1;

    /// <summary>
    /// Whether this event has been completed or not.
    /// </summary>
    public bool Finished => Stage >= TotalStages;

    /// <summary>
    /// Safely sets the stage of this event, ensuring that the stage does not go down.
    /// </summary>
    public void SafeSetStage(int value)
    {
        if (Stage < value)
        {
            Stage = value;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                PacketManager.SendPacket<EventStageUpdatedPacket>(GetType().FullName!, Stage);
            }
        }
    }

    public sealed override void PreUpdateNPCs()
    {
        int solynIndex = NPC.FindFirstNPC(ModContent.NPCType<Solyn>());
        if (solynIndex == -1)
            Solyn = null;
        else
            Solyn = Main.npc[solynIndex].As<Solyn>();
    }

    public override void OnWorldLoad()
    {
        if (!SubworldSystem.AnyActive())
            Stage = 0;
    }

    public override void OnWorldUnload()
    {
        if (!SubworldSystem.AnyActive())
            Stage = 0;
    }

    public override void NetSend(BinaryWriter writer) => writer.Write(Stage);

    public override void NetReceive(BinaryReader reader) => Stage = reader.ReadInt32();

    public override void SaveWorldData(TagCompound tag) => tag["Stage"] = Stage;

    public override void LoadWorldData(TagCompound tag) => Stage = tag.GetInt("Stage");
}
