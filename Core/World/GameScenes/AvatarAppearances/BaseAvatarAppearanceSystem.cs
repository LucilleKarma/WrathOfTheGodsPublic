using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.World.GameScenes.AvatarAppearances;

public abstract class BaseAvatarAppearanceSystem : ModSystem
{
    /// <summary>
    /// Whether the event is waiting to start. This does <b>not</b> indicate that the appearance is ongoing, merely that it's waiting for a good opportunity to strike.
    /// </summary>
    public bool WaitingToStart
    {
        get;
        set;
    }

    /// <summary>
    /// How long it's been since the event started.
    /// </summary>
    public int EventTimer
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the event is ongoing.
    /// </summary>
    public bool Ongoing => EventTimer >= 1;

    public override void OnWorldLoad()
    {
        WaitingToStart = false;
        EventTimer = 0;
        RiftEclipseSky.IsEnabled = false;
    }

    public override void OnWorldUnload()
    {
        WaitingToStart = false;
        EventTimer = 0;
        RiftEclipseSky.IsEnabled = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag[nameof(WaitingToStart) + Name] = WaitingToStart;
        tag[nameof(EventTimer) + Name] = EventTimer;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        WaitingToStart = tag.GetBool(nameof(WaitingToStart) + Name);
        EventTimer = tag.GetInt(nameof(EventTimer) + Name);
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)(WaitingToStart ? 1 : 0));
        writer.Write(EventTimer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        WaitingToStart = reader.ReadByte() != 0;
        EventTimer = reader.ReadInt32();
    }

    public override void PostUpdateEverything()
    {
        TryToStartEvent();
        if (Ongoing)
            UpdateEvent();
    }

    public virtual void TryToStartEvent()
    {
        // Don't bother performing checks if client-side or not waiting on anything.
        if (Main.netMode == NetmodeID.MultiplayerClient || !WaitingToStart)
            return;

        Player closestToSurface = Main.player[Player.FindClosest(new(Main.maxTilesX * 8f, 3000f), 1, 1)];

        // Perform an RNG check if the event can start.
        bool onSurface = closestToSurface.Center.Y <= Main.worldSurface * 16f - 200f;
        bool eventCanStart = onSurface && !AnyBosses() && Main.dayTime && !CreditsRollEvent.IsEventOngoing;
        if (!eventCanStart || !Main.rand.NextBool(240))
            return;

        // The event has started, disable the wait flag to reflect this.
        WaitingToStart = false;

        // Start the event timer.
        EventTimer = 1;

        // Fire a world update from the server.
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }

    public abstract void UpdateEvent();
}
