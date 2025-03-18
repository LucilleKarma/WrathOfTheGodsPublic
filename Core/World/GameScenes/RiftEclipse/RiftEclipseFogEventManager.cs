using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Enemies.RiftEclipse;
using NoxusBoss.Core.Autoloaders;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.World.GameScenes.RiftEclipse;

public class RiftEclipseFogEventScene : ModSceneEffect
{
    public override ModWaterStyle WaterStyle => ModContent.Find<ModWaterStyle>("NoxusBoss/RiftEclipseFogWater");

    public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;

    public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/RiftEclipseFog");

    public override void Load()
    {
        string contentPath = GetAssetPath("Content/Items/Placeable/MusicBoxes", "RiftEclipseFogMusicBox");
        string musicPath = "Assets/Sounds/Music/RiftEclipseFog";
        MusicBoxAutoloader.Create(Mod, contentPath, musicPath, out _, out _, AvatarRift.DrawMusicBoxWithBackRift);
    }

    public override bool IsSceneEffectActive(Player player) => RiftEclipseFogEventManager.EventOngoing && player.position.Y <= Main.worldSurface * 16f + 1640f;
}

public class RiftEclipseFogEventManager : ModSystem
{
    /// <summary>
    /// How long the fog has existed since it started.
    /// </summary>
    public static int FogTime
    {
        get;
        set;
    }

    /// <summary>
    /// How long the fog should persist.
    /// </summary>
    public static int FogDuration
    {
        get;
        set;
    }

    /// <summary>
    /// A countdown for how much time needs to pass before the fog can potentially start again. This is set after fog ends to prevent it from reappearing suddenly.
    /// </summary>
    public static int FogRestartCooldown
    {
        get;
        set;
    }

    /// <summary>
    /// Whether fog is ongoing.
    /// </summary>
    public static bool EventOngoing => FogTime >= 1;

    /// <summary>
    /// Whether the fog can appear.
    /// </summary>
    public static bool EventCanHappen => CommonCalamityVariables.DevourerOfGodsDefeated && RiftEclipseManagementSystem.RiftEclipseOngoing;

    /// <summary>
    /// The 0-1 ratio for how complete the fog is.
    /// </summary>
    public static float FogCompletionRatio
    {
        get
        {
            if (FogDuration == 0)
                return 0f;

            return FogTime / (float)FogDuration;
        }
    }

    /// <summary>
    /// The 0-1 intensity of the fog visuals.
    /// </summary>
    public static float FogDrawIntensity => InverseLerpBump(0f, 0.023f, 0.9f, 1f, FogCompletionRatio) * (1f - AnyBosses().ToInt());

    /// <summary>
    /// The minimum duration for the fog event.
    /// </summary>
    public static readonly int MinFogDuration = MinutesToFrames(1f);

    /// <summary>
    /// The maximum duration for the fog event.
    /// </summary>
    public static readonly int MaxFogDuration = MinutesToFrames(2.2f);

    /// <summary>
    /// The chance for fog ambience to play on a given frame.
    /// </summary>
    public static readonly int FogAmbiencePlayChance = MinutesToFrames(1.25f);

    /// <summary>
    /// The chance of the fog starting on a given frame.
    /// </summary>
    public static readonly int FogStartChance = MinutesToFrames(27f);

    /// <summary>
    /// How long the game has to wait before the fog can happen again, so that it doesn't (albeit very rarely) happen shortly after a previous fog event.
    /// </summary>
    public static readonly int FogRestartDelay = MinutesToFrames(240f);

    public override void OnModLoad()
    {
        // Load the fog shader.
        if (Main.netMode != NetmodeID.Server)
        {
            Asset<Effect> s = Mod.Assets.Request<Effect>("Assets/AutoloadedEffects/Shaders/SkyAndZoneEffects/DarkFogScreenShader");
            Filters.Scene["NoxusBoss:NoxusWorldFog"] = new Filter(new RiftEclipseFogShaderData(s, ManagedShader.DefaultPassName).UseColor(Color.Transparent).UseOpacity(0f), EffectPriority.VeryHigh);
        }

        // Subscribe to events.
        GlobalNPCEventHandlers.EditSpawnRateEvent += EditSpawnRates;
        GlobalNPCEventHandlers.EditSpawnPoolEvent += EditSpawnPool;

        base.OnModLoad();
    }

    public override void PostUpdateNPCs()
    {
        // Count down the restart countdown.
        FogRestartCooldown = Utils.Clamp(FogRestartCooldown - 1, 0, FogRestartDelay);

        // Randomly try to start the event. This cannot happen if the restart delay is counting down, there's a boss present, if no one has 200 or more max HP, or at night/a regular eclipse is ongoing.
        // Lastly, it cannot happen if the base condition is not met.
        if (EventCanHappen && FogRestartCooldown <= 0 && Main.rand.NextBool(FogStartChance) && !AnyBosses() && Main.CanStartInvasion(0, true) && Main.dayTime && !Main.eclipse)
            Start();

        UpdateEvent();
    }

    public override void OnWorldLoad()
    {
        FogTime = 0;
        FogDuration = 0;
        FogRestartCooldown = 0;
    }

    public override void OnWorldUnload()
    {
        FogTime = 0;
        FogDuration = 0;
        FogRestartCooldown = 0;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["FogTime"] = FogTime;
        tag["FogDuration"] = FogDuration;
        tag["FogRestartCooldown"] = FogRestartCooldown;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        FogTime = tag.GetInt("FogTime");
        FogDuration = tag.GetInt("FogDuration");
        FogRestartCooldown = tag.GetInt("FogRestartCooldown");
    }

    private void EditSpawnRates(Player player, ref int spawnRate, ref int maxSpawns)
    {
        // Make spawn rates gradually go down in accordance with the draw intensity.
        // Once the draw intensity is at its maximum, however, this stops and default spawns are instead disabled via the other
        // event, in the form of clearing the spawn pool.
        if (FogDrawIntensity > 0f && FogDrawIntensity < 1f)
        {
            spawnRate = (int)(spawnRate * Lerp(1f, 15f, FogDrawIntensity));
            maxSpawns = (int)(maxSpawns * (1f - FogDrawIntensity));
        }

        // Once the intensity is at its maximum, the spawn rates must be configured for custom spawns.
        if (FogDrawIntensity >= 1f)
        {
            // spawnRate corresponds to the value in Main.rand.NextBool(x) when deciding if an NPC should be spawned.
            // The intent is to configure this probability such that there's a 95% chance for an NPC to spawn in the duration of the fog.
            float spawnChanceThroughEvent = 0.95f;

            // The probability of N successive rolls of chance P occurring is as follows:
            // 1 - (1 - P)^N
            // Substituting spawnChanceThroughEvent for q, we wish to solve this equation as such:
            // 1 - (1 - x)^N = q

            // Solving it is simply a matter of rearranging terms:
            // 1 - (1 - x)^N = q
            // (1 - x)^N = 1 - q
            // ln((1 - x)^N) = ln(1 - q)
            // N * ln(1 - x) = ln(1 - q)
            // ln(1 - x) = ln(1 - q) / N
            // 1 - x = exp(ln(1 - q) / N)
            // x = 1 - exp(ln(1 - q) / N)
            // x = 1 - exp(ln(1 - q) ^ (1 / N))
            // x = 1 - (1 - q) ^ (1 / N)
            float spawnRateProbability = 1f - Pow(1f - spawnChanceThroughEvent, 1f / FogDuration);

            // Lastly, since spawnRateProbability is a 0-1 probability, it's necessary to perform 1 / spawnRateProbability to get the odds denominator.
            spawnRate = (int)Round(1f / spawnRateProbability);

            // Ensure that only one enemy may exist at any given time.
            maxSpawns = 1;
        }
    }

    private void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (FogDrawIntensity < 1f)
            return;

        // Disable default spawns.
        pool.Clear();

        if (spawnInfo.Player.ZoneForest)
            pool[ModContent.NPCType<Fogwood>()] = 1f;
        pool[ModContent.NPCType<Mirrorwalker>()] = 1f;
    }

    public static void Start()
    {
        if (Main.LocalPlayer.Center.Y <= Main.worldSurface * 16f + 160f)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.RiftEclipse.RiftEclipseFogStart);
            ScreenShakeSystem.StartShake(4f);
        }

        FogTime = 1;
        FogDuration = Main.rand.Next(MinFogDuration, MaxFogDuration + 1);

        // Sync the fog time.
        if (Main.netMode == NetmodeID.Server)
            PacketManager.SendPacket<RiftEclipseFogTimePacket>();
    }

    public static void IncrementTime(int timeIncrement)
    {
        FogTime += timeIncrement;

        // If time increments are significant, send a packet to ensure that everyone is on the same page.
        bool sendPacket = timeIncrement >= 120;

        // Every so often send a packet to ensure that the timer doesn't drift off for anyone.
        if (FogTime % 900 == 899)
            sendPacket = true;

        // Make the event stop and initiate the restart cooldown once it has completed.
        if (Main.netMode != NetmodeID.MultiplayerClient && FogTime >= FogDuration)
        {
            FogTime = 0;
            FogRestartCooldown = FogRestartDelay;
            sendPacket = true;
        }

        if (sendPacket && Main.netMode == NetmodeID.Server)
            PacketManager.SendPacket<RiftEclipseFogTimePacket>();
    }

    public static void UpdateEvent()
    {
        // Don't do anything if the event is not ongoing.
        if (!EventOngoing)
            return;

        // Stop rain.
        Main.StopRain();
        Main.raining = false;

        // Randomly play ambience.
        if (Main.rand.NextBool(FogAmbiencePlayChance))
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.RiftEclipse.RiftEclipseFogAmbience with { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, Volume = 0.7f });
            ScreenShakeSystem.StartShake(4f, shakeStrengthDissipationIncrement: 0.016f);
        }

        // Update the fog timer.
        IncrementTime(1);
    }
}
