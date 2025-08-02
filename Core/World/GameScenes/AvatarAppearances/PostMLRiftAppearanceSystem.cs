using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.World.GameScenes.AvatarAppearances;

public class PostMLRiftAppearanceSystem : BaseAvatarAppearanceSystem
{
    /// <summary>
    /// Whether the Avatar has covered the moon at one point.
    /// </summary>
    public static bool AvatarHasCoveredMoon
    {
        get;
        set;
    }

    public override void OnModLoad()
    {
        // Load events.
        GlobalNPCEventHandlers.OnKillEvent += InitiateWaitAfterML;
        GlobalNPCEventHandlers.EditSpawnRateEvent += DisableSpawnRates;
    }

    public override void OnModUnload()
    {
        GlobalNPCEventHandlers.OnKillEvent -= InitiateWaitAfterML;
        GlobalNPCEventHandlers.EditSpawnRateEvent -= DisableSpawnRates;
    }

    public override void OnWorldUnload()
    {
        AvatarHasCoveredMoon = false;
        base.OnWorldUnload();
    }

    public override void OnWorldLoad()
    {
        AvatarHasCoveredMoon = false;
        base.OnWorldLoad();
    }

    private void InitiateWaitAfterML(NPC npc)
    {
        // Start the appearance wait after the Moon Lord is defeated.
        if (npc.type == NPCID.MoonLordCore && !AvatarHasCoveredMoon && !BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>())
            WaitingToStart = true;
    }

    private void DisableSpawnRates(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (Ongoing)
        {
            spawnRate = 0;
            maxSpawns = 0;
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        base.NetSend(writer);
        writer.Write(AvatarHasCoveredMoon);
    }

    public override void NetReceive(BinaryReader reader)
    {
        base.NetReceive(reader);
        AvatarHasCoveredMoon = reader.ReadBoolean();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        base.SaveWorldData(tag);
        if (AvatarHasCoveredMoon)
            tag["RiftHasCoveredMoon"] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        base.LoadWorldData(tag);
        AvatarHasCoveredMoon = tag.ContainsKey("RiftHasCoveredMoon");
    }

    public override void UpdateEvent()
    {
        int animationDelay = 60;
        int giantRiftAppearTime = 90;
        int giantRiftExpandOffTime = 660;
        int ambienceDuration = 1804;

        // Get rid of all falling stars. Their noises completely ruin the ambience.
        // active = false must be used over Kill because the Kill method causes them to drop their fallen star items.
        IEnumerable<Projectile> fallingStars = AllProjectilesByID(ProjectileID.FallingStar);
        foreach (Projectile star in fallingStars)
            star.active = false;

        // Ensure that the player hears the music.
        if (Main.musicVolume < 0.15f)
            Main.musicVolume = 0.15f;

        // Make the rift appear in the background.
        RiftEclipseSky.IsEnabled = true;

        // Make the Avatar's rift invisible at first.
        if (EventTimer < animationDelay)
        {
            for (int i = 0; i < Main.musicFade.Length; i++)
            {
                if (i != Main.curMusic)
                    Main.musicFade[i] *= 0.94f;
            }
            RiftEclipseSky.RiftScaleFactor = 0.000001f;
        }
        else
        {
            if (EventTimer == animationDelay + giantRiftAppearTime + (int)(giantRiftExpandOffTime * 0.4f))
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.Environment.RiftEclipseStart).WithVolumeBoost(1.95f);
                ScreenShakeSystem.StartShake(3f);
            }

            float expandCompletion = InverseLerp(giantRiftAppearTime, giantRiftAppearTime + giantRiftExpandOffTime, EventTimer - animationDelay);
            RiftEclipseSky.RiftScaleFactor = RiftEclipseSky.ScaleWhenOverSun * EasingCurves.Cubic.Evaluate(EasingType.InOut, expandCompletion);
            RiftEclipseSky.MoveOverSunInterpolant = 0.999f;
            RiftEclipseSky.ReducedHorizontalOffset = true;
        }

        // Increment the event timer.
        EventTimer++;
        if (EventTimer >= ambienceDuration - 180)
        {
            RiftEclipseSky.MoveOverSunInterpolant = 1f;
            RiftEclipseSky.ReducedHorizontalOffset = false;
            AvatarHasCoveredMoon = true;
            EventTimer = 0;
        }
    }
}
