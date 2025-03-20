using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving;
using SubworldLibrary;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.GameScenes.SolynEventHandlers;

public class RandomSolynSpawnSystem : ModSystem
{
    public static bool ShouldSpawn
    {
        get
        {
            // She's not coming back...
            if (BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>())
                return false;

            // What?
            if (SubworldSystem.AnyActive())
                return false;

            // Obviously, Solyn cannot spawn twice.
            if (NPC.AnyNPCs(ModContent.NPCType<Solyn>()))
                return false;

            // Prevent spawning during night-time.
            // This is inverted the first time Solyn shows up, where she falls from the sky like a fallen star.
            if (!Main.dayTime && SolynHasAppearedBefore || Main.dayTime && !SolynHasAppearedBefore)
                return false;

            // Prevent spawning if the right bosses aren't defeated yet.
            if (!SufficientDownedFlagsForSpawning)
                return false;

            // Prevent spawning if it's close to night time anyway.
            // The exact cutoff time for this is 4:00PM.
            int fourPM = (int)Main.dayLength - 12600;
            if (Main.time >= fourPM && SolynHasAppearedBefore)
                return false;

            // Prepare a dice roll. This is done to minimize the amount of times more complicated checks happen below.
            if (!Main.rand.NextBool(SpawnChancePerFrame))
                return false;

            // Prevent spawning if a boss or invasion is present.
            if (AnyInvasionsOrEvents() || AnyBosses())
                return false;

            return true;
        }
    }

    /// <summary>
    /// Whether Solyn has appeared in the world before or not.
    /// </summary>
    public static bool SolynHasAppearedBefore
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Solyn has been spoken to before or not.
    /// </summary>
    public static bool SolynHasBeenSpokenTo
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the relevant downed boss flags have been triggered in order for Solyn to appear. This requires that Skeletron or the Wall of Flesh be defeated.
    /// </summary>
    public static bool SufficientDownedFlagsForSpawning => NPC.downedBoss3 || Main.hardMode;

    /// <summary>
    /// The chance of Solyn being able to spawn every frame.
    /// </summary>
    public static int SpawnChancePerFrame => MinutesToFrames(0.03f);

    public override void OnWorldLoad() => SolynHasAppearedBefore = false;

    public override void OnWorldUnload() => SolynHasAppearedBefore = false;

    public override void PreUpdateEntities()
    {
        // Attempt to summon Solyn if required.
        // This does not happen in old worlds.
        if (Main.netMode != NetmodeID.MultiplayerClient && !WorldVersionSystem.PreAvatarUpdateWorld)
        {
            bool solynAlreadyExists = NPC.AnyNPCs(ModContent.NPCType<Solyn>()) || NPC.AnyNPCs(ModContent.NPCType<BattleSolyn>());
            if (SolynCampsiteWorldGen.TentPosition != Vector2.Zero && SolynHasAppearedBefore && !BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() && !solynAlreadyExists)
                RespawnSolynAtCampsite();
            else if (!SolynHasAppearedBefore && ShouldSpawn)
            {
                Player closest = Main.player[Player.FindClosest(new(Main.maxTilesX * 8, 3000f), 1, 1)];
                bool wideOpenArea = !Collision.SolidCollision(closest.Center - new Vector2(400f, 750f), 800, 650);
                if (wideOpenArea)
                    SummonSolynAsFallingHighVelocityCelestialObjectBecauseWhyNot(closest);
            }
        }
    }

    public static void SummonSolynAsFallingHighVelocityCelestialObjectBecauseWhyNot(Player player)
    {
        Vector2 spawnPosition = player.Center + new Vector2(player.direction * -200f, -1000f);
        int solyn = NPC.NewNPC(new EntitySource_WorldEvent(), (int)spawnPosition.X, (int)spawnPosition.Y, ModContent.NPCType<Solyn>(), 1, Target: player.whoAmI);
        if (Main.npc.IndexInRange(solyn))
        {
            Main.npc[solyn].As<Solyn>().CurrentState = SolynAIType.FallFromTheSky;
            Main.npc[solyn].velocity = new Vector2(player.direction * 7.5f, 6f);
        }
    }

    public static void RespawnSolynAtCampsite()
    {
        // Make Solyn appear.
        Vector2 spawnPosition = SolynCampsiteWorldGen.TentPosition + new Vector2(30f, 8f);
        NPC.NewNPC(new EntitySource_WorldEvent(), (int)spawnPosition.X, (int)spawnPosition.Y, ModContent.NPCType<Solyn>());
    }

    /// <summary>
    /// Checks if any invasions or events, such as the eclipse, blood moon, private invasion, acid rain, etc. are ongoing.
    /// </summary>
    public static bool AnyInvasionsOrEvents()
    {
        // Check if there is an invasion ongoing, such as goblins or pirates.
        if (Main.invasionType > 0 && Main.invasionProgressNearInvasion)
            return true;

        // Check if the pillars are present.
        if (NPC.LunarApocalypseIsUp)
            return true;

        // Check if the Old One's Army is ongoing.
        if (DD2Event.Ongoing)
            return true;

        // Check if an eclipse or special moon is ongoing.
        if (Main.eclipse || Main.pumpkinMoon || Main.snowMoon || Main.bloodMoon)
            return true;

        // Check if the Acid Rain event is ongoing.
        if (CommonCalamityVariables.AcidRainIsOngoing)
            return true;

        return false;
    }
}
