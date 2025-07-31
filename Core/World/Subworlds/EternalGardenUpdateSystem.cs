using CalamityMod.Systems;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.FormPresets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Content.Projectiles.Visuals;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.Players;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving;
using ReLogic.Utilities;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Core.World.Subworlds.EternalGardenNew;

namespace NoxusBoss.Core.World.Subworlds;

public class EternalGardenUpdateSystem : ModSystem
{
    public static int CrystalCrusherRayID
    {
        get;
        private set;
    } = -10000;

    public static int MortarRoundID
    {
        get;
        private set;
    } = -10000;

    public static int RubberMortarRoundID
    {
        get;
        private set;
    } = -10000;

    /// <summary>
    /// Whether the current client/server was in the Eternal Garden subworld in the last frame.
    /// </summary>
    public static bool WasInSubworldLastUpdateFrame
    {
        get;
        private set;
    }

    /// <summary>
    /// A timer that determines how light any player has spent bathing in the light without moonscreen. This dictates when Nameless spawns.
    /// </summary>
    public static int TimeSpentInLight
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether any players are standing beneath the tree and basking in the light without moonscreen, and thus preparing for the summoning of Nameless.
    /// </summary>
    public static bool AnyoneTryingToSummonNameless
    {
        get;
        private set;
    }

    /// <summary>
    /// The sound loop instance for the bask-in-the-light visual.
    /// </summary>
    public static SlotId StandingInCenterSound
    {
        get;
        private set;
    }

    /// <summary>
    /// How long players must wait in the center of the garden in order for Nameless to appear.
    /// </summary>
    public static readonly int NamelessDeitySummonDelayInCenter = SecondsToFrames(5f);

    /// <summary>
    /// The post-processing effect that handles the light appearance visual.
    /// </summary>
    public static readonly PlayerPostProcessingEffect LightFadeInEffect = new PlayerPostProcessingEffect(player =>
    {
        float materializeInterpolant = SmoothStep(0f, 1f, Pow(InverseLerp(0f, 0.5f, player.GetValueRef<float>(PlayerLightFadeInInterpolantName)), 0.9f));
        float fadeToWhite = InverseLerp(0.95f, 0.55f, player.GetValueRef<float>(PlayerLightFadeInInterpolantName)).Squared();

        ManagedShader lightShader = ShaderManager.GetShader("NoxusBoss.LightMaterializationShader");
        lightShader.TrySetParameter("materializeInterpolant", materializeInterpolant);
        lightShader.TrySetParameter("fadeToWhite", fadeToWhite);
        lightShader.TrySetParameter("baseTextureSize", Main.ScreenSize.ToVector2());
        lightShader.SetTexture(DendriticNoise, 1, SamplerState.PointWrap);
        lightShader.Apply();
    }, false);

    /// <summary>
    /// The name of the variable used to determine how much the player has faded in into the garden.
    /// </summary>
    public const string PlayerLightFadeInInterpolantName = "GardenLightFadeInInterpolant";

    public override void OnModLoad()
    {
        // Subscribe to various events.
        PlayerDataManager.PostUpdateEvent += CreatePaleDuckweedInGarden;
        PlayerDataManager.ResetEffectsEvent += DisallowPlantBreakageInGarden;
        PlayerDataManager.PostUpdateEvent += CreateWindInGarden;
        GlobalItemEventHandlers.CanUseItemEvent += DisableCelestialSigil;
        GlobalItemEventHandlers.CanUseItemEvent += DisableProblematicItems;
        GlobalNPCEventHandlers.EditSpawnPoolEvent += OnlyAllowFriendlySpawnsInGarden;
        GlobalNPCEventHandlers.EditSpawnRateEvent += IncreaseFriendlySpawnsInGarden;
        GlobalProjectileEventHandlers.PreAIEvent += KillProblematicProjectilesInGarden;
        GlobalTileEventHandlers.NearbyEffectsEvent += MakeTombsGo1984InGarden;
        GlobalTileEventHandlers.IsTileUnbreakableEvent += DisallowTileBreakageInGarden;
        GlobalWallEventHandlers.IsWallUnbreakableEvent += DisallowWallBreakageInGarden;

        Main.OnPreDraw += HandleFadeIns;
        On_Player.PlaceThing_Walls += DisableWallPlacementInGarden;
        On_Player.PlaceThing_Tiles += DisableTilePlacementInGarden;
    }

    private void HandleFadeIns(GameTime obj)
    {
        foreach (Player player in Main.ActivePlayers)
        {
            Referenced<float> fadeInInterpolant = player.GetValueRef<float>(PlayerLightFadeInInterpolantName);
            if (fadeInInterpolant <= 0f || fadeInInterpolant >= 1f)
                return;

            PlayerPostProcessingShaderSystem.ApplyPostProcessingEffect(player, LightFadeInEffect);

            if (!Main.gamePaused)
                fadeInInterpolant.Value += 0.014f;
            if (fadeInInterpolant >= 1f)
                fadeInInterpolant.Value = 0f;
        }
    }

    public override void PostSetupContent()
    {
        if (ModReferences.Calamity is not null)
        {
            if (ModReferences.Calamity.TryFind("CrystylCrusherRay", out ModProjectile ray))
                CrystalCrusherRayID = ray.Type;
            if (ModReferences.Calamity.TryFind("MortarRoundProj", out ModProjectile mortar))
                MortarRoundID = mortar.Type;
            if (ModReferences.Calamity.TryFind("RubberMortarRoundProj", out ModProjectile rubberMortar))
                RubberMortarRoundID = rubberMortar.Type;
        }
    }

    private void CreatePaleDuckweedInGarden(PlayerDataManager p)
    {
        // Create pale duckweed in the water if the player is in the eternal garden and Nameless is not present.
        int duckweedSpawnChance = 3;
        if (!WasInSubworldLastUpdateFrame || NamelessDeityBoss.Myself is not null || !Main.rand.NextBool(duckweedSpawnChance))
            return;

        // Try to find a suitable location to spawn the duckweed. Once one is found, this loop terminates.
        // If none is found, then the loop simply runs through all its iterations without issue.
        for (int tries = 0; tries < 50; tries++)
        {
            Vector2 potentialSpawnPosition = p.Player.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(200f, 1500f);
            if (Collision.SolidCollision(potentialSpawnPosition, 1, 1) || !Collision.WetCollision(potentialSpawnPosition, 1, 1))
                continue;

            Vector2 spawnVelocity = -Vector2.UnitY.RotatedByRandom(0.82f) * Main.rand.NextFloat(0.5f, 1.35f);
            Color duckweedColor = Color.Lerp(Color.Wheat, Color.Red, Main.rand.NextFloat(0.52f));
            PaleDuckweedParticle duckweed = new PaleDuckweedParticle(potentialSpawnPosition, spawnVelocity, duckweedColor, 540);
            duckweed.Spawn();
            break;
        }
    }

    private void DisallowPlantBreakageInGarden(PlayerDataManager p)
    {
        // Prevent players from breaking the plants with swords and projectiles in the subworld.
        if (WasInSubworldLastUpdateFrame)
            p.Player.dontHurtNature = true;
    }

    private void CreateWindInGarden(PlayerDataManager p)
    {
        if (Main.myPlayer == p.Player.whoAmI && WasInSubworldLastUpdateFrame && NamelessDeityBoss.Myself is null && Main.rand.NextBool(9))
        {
            Vector2 windVelocity = Vector2.UnitX * Main.rand.NextFloat(10f, 14f) * Main.windSpeedTarget;

            // Try to find a suitable location to spawn the wind. Once one is found, this loop terminates.
            // If none is found, then the loop simply runs through all its iterations without issue.
            for (int tries = 0; tries < 50; tries++)
            {
                Vector2 potentialSpawnPosition = p.Player.Center + new Vector2(Sign(windVelocity.X) * -Main.rand.NextFloat(1050f, 1250f), Main.rand.NextFloatDirection() * 900f);
                if (Collision.SolidCollision(potentialSpawnPosition, 1, 120) || Collision.WetCollision(potentialSpawnPosition, 1, 120))
                    continue;

                Projectile.NewProjectile(p.Player.GetSource_FromThis(), potentialSpawnPosition, windVelocity, ModContent.ProjectileType<WindStreakVisual>(), 0, 0f, p.Player.whoAmI);
                break;
            }
        }
    }

    private bool DisableCelestialSigil(Item item, Player player)
    {
        if (!WasInSubworldLastUpdateFrame)
            return true;

        return item.type != ItemID.CelestialSigil;
    }

    private bool DisableProblematicItems(Item item, Player player)
    {
        if (!WasInSubworldLastUpdateFrame)
            return true;

        // Disable liquid placing/removing items.
        int itemID = item.type;
        bool isSponge = itemID == ItemID.SuperAbsorbantSponge || itemID == ItemID.LavaAbsorbantSponge || itemID == ItemID.HoneyAbsorbantSponge || itemID == ItemID.UltraAbsorbantSponge;
        bool isRegularBucket = itemID == ItemID.EmptyBucket || itemID == ItemID.WaterBucket || itemID == ItemID.LavaBucket || itemID == ItemID.HoneyBucket;
        bool isSpecialBucket = itemID == ItemID.BottomlessBucket || itemID == ItemID.BottomlessLavaBucket || itemID == ItemID.BottomlessHoneyBucket || itemID == ItemID.BottomlessShimmerBucket;
        return !isSponge && !isRegularBucket && !isSpecialBucket;
    }

    private void OnlyAllowFriendlySpawnsInGarden(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (!WasInSubworldLastUpdateFrame)
            return;

        // Get a collection of all NPC IDs in the spawn pool that are not critters.
        IEnumerable<int> npcsToRemove = pool.Keys.Where(npcID => !NPCID.Sets.CountsAsCritter[npcID]);

        // Use the above collection as a blacklist, removing all NPCs that are included in it, effectively ensuring only critters may spawn in the garden.
        foreach (int npcIDToRemove in npcsToRemove)
            pool.Remove(npcIDToRemove);
    }

    private void IncreaseFriendlySpawnsInGarden(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (WasInSubworldLastUpdateFrame)
        {
            spawnRate = 60;
            maxSpawns = NamelessDeityBoss.Myself is null ? 50 : 0;
        }
    }

    private bool KillProblematicProjectilesInGarden(Projectile projectile)
    {
        // Don't do anything if this event is called outside of the garden.
        if (!WasInSubworldLastUpdateFrame)
            return true;

        // This apparently causes shader issues in the garden.
        // This projectile is notably used by the Shattered Community when leveling up.
        if (projectile.type == ProjectileID.DD2ElderWins)
        {
            projectile.active = false;
            return false;
        }

        // Prevent tombs from cluttering things up in the garden.
        bool isTomb = projectile.type is ProjectileID.Tombstone or ProjectileID.Gravestone or ProjectileID.RichGravestone1 or ProjectileID.RichGravestone2 or
            ProjectileID.RichGravestone3 or ProjectileID.RichGravestone4 or ProjectileID.RichGravestone4 or ProjectileID.Headstone or ProjectileID.Obelisk or
            ProjectileID.GraveMarker or ProjectileID.CrossGraveMarker or ProjectileID.Headstone;
        if (isTomb)
            projectile.active = false;

        // Prevent crystyl crusher's beam and other tile-manipulating items like the sandgun from working in the garden and messing up tiles.
        if (projectile.type == CrystalCrusherRayID)
            projectile.active = false;
        if (projectile.type == ProjectileID.DirtBomb || projectile.type == ProjectileID.DirtStickyBomb)
            projectile.active = false;
        if (projectile.type == ProjectileID.SandBallGun || projectile.type == ProjectileID.SandBallGun)
            projectile.active = false;
        if (projectile.type == ProjectileID.SandBallFalling || projectile.type == ProjectileID.PearlSandBallFalling)
            projectile.active = false;
        if (projectile.type == ProjectileID.EbonsandBallFalling || projectile.type == ProjectileID.EbonsandBallGun)
            projectile.active = false;
        if (projectile.type == ProjectileID.CrimsandBallFalling || projectile.type == ProjectileID.CrimsandBallGun)
            projectile.active = false;

        // From the Dirt Rod. Kill is used instead of active = false to ensure that the dirt doesn't just vanish and gets placed down again in its original location.
        if (projectile.type == ProjectileID.DirtBall)
            projectile.Kill();

        // No explosives.
        // MAN rocket code is evil!
        bool dryRocket = projectile.type == ProjectileID.DryRocket || projectile.type == ProjectileID.DrySnowmanRocket;
        bool wetRocket = projectile.type == ProjectileID.WetRocket || projectile.type == ProjectileID.WetSnowmanRocket;
        bool honeyRocket = projectile.type == ProjectileID.HoneyRocket || projectile.type == ProjectileID.HoneySnowmanRocket;
        bool lavaRocket = projectile.type == ProjectileID.LavaRocket || projectile.type == ProjectileID.LavaSnowmanRocket;
        bool rocket = dryRocket || wetRocket || honeyRocket || lavaRocket || projectile.type == MortarRoundID || projectile.type == RubberMortarRoundID;

        bool dryMisc = projectile.type == ProjectileID.DryGrenade || projectile.type == ProjectileID.DryMine;
        bool wetMisc = projectile.type == ProjectileID.WetGrenade || projectile.type == ProjectileID.WetMine;
        bool honeyMisc = projectile.type == ProjectileID.HoneyGrenade || projectile.type == ProjectileID.HoneyMine;
        bool lavaMisc = projectile.type == ProjectileID.LavaGrenade || projectile.type == ProjectileID.LavaMine;
        bool miscExplosive = dryMisc || wetMisc || honeyMisc || lavaMisc;

        // No clentaminator sprays either.
        bool clentaminatorA = projectile.type == ProjectileID.CorruptSpray || projectile.type == ProjectileID.CrimsonSpray || projectile.type == ProjectileID.HallowSpray || projectile.type == ProjectileID.DirtSpray;
        bool clentaminatorB = projectile.type == ProjectileID.MushroomSpray || projectile.type == ProjectileID.SandSpray || projectile.type == ProjectileID.SnowSpray || projectile.type == ProjectileID.PureSpray;

        if (rocket || miscExplosive || clentaminatorA || clentaminatorB)
        {
            projectile.active = false;
            return false;
        }

        return true;
    }

    private void MakeTombsGo1984InGarden(int x, int y, int type, bool closer)
    {
        if (!WasInSubworldLastUpdateFrame)
            return;

        // Erase tombstones in the garden.
        if (type == TileID.Tombstones)
            Main.tile[x, y].Get<TileWallWireStateData>().HasTile = false;
    }

    private bool DisallowTileBreakageInGarden(int x, int y, int type)
    {
        // True = Tiles are unbreakable, False = Tiles are breakable.
        if (type == ModContent.TileType<GoodAppleTree>() && !Main.tile[x, y].IsActuated)
            return false;

        if (type == ModContent.TileType<LotusOfCreationTile>())
            return false;

        return WasInSubworldLastUpdateFrame;
    }

    private bool DisallowWallBreakageInGarden(int x, int y, int type)
    {
        // True = Walls are unbreakable, False = Walls are breakable.
        return WasInSubworldLastUpdateFrame;
    }

    private void DisableTilePlacementInGarden(On_Player.orig_PlaceThing_Tiles orig, Player self)
    {
        if (!WasInSubworldLastUpdateFrame)
            orig(self);
    }

    private void DisableWallPlacementInGarden(On_Player.orig_PlaceThing_Walls orig, Player self)
    {
        if (!WasInSubworldLastUpdateFrame)
            orig(self);
    }

    public override void PreUpdateEntities()
    {
        // Reset the text opacity when the game is being played. It will increase up to full opacity during subworld transition drawing.
        TextOpacity = 0f;

        // Verify whether things are in the subworld. This hook runs on both clients and the server. If for some reason this stuff needs to be determined in a different
        // hook it is necessary to ensure that property is preserved wherever you put it.
        bool inGarden = SubworldSystem.IsActive<EternalGardenNew>();
        if (WasInSubworldLastUpdateFrame != inGarden)
        {
            // A major flaw with respect to subworld data transfer is the fact that Calamity's regular OnWorldLoad hooks clear everything.
            // This works well and good for Calamity's purposes, but it causes serious issues when going between subworlds. The result of this is
            // ordered as follows:

            // 1. Exit world. Store necessary data for subworld transfer.
            // 2. Load necessary stuff for subworld and wait.
            // 3. Enter subworld. Load data from step 1.
            // 4. Call OnWorldLoad, resetting everything from step 3.

            // In order to address this, a final step is introduced:
            // 5. Load data from step 3 again on the first frame of entity updating.
            if (inGarden)
            {
                if (Main.netMode != NetmodeID.Server)
                    LoadWorldDataFromTag("Client", ClientWorldDataTag);

                StartPlayerFadeInEffects();

                foreach (TileEntity te in TileEntity.ByPosition.Values)
                {
                    if (te is TEGoodAppleTree tree)
                        tree.RegenerateApples();
                }
            }

            WasInSubworldLastUpdateFrame = inGarden;
        }

        // Everything beyond this point applies solely to the subworld.
        if (!WasInSubworldLastUpdateFrame)
        {
            TimeSpentInLight = 0;
            return;
        }

        // Apply subworld specific behaviors.
        SubworldSpecificUpdateBehaviors();
    }

    private static void SubworldSpecificUpdateBehaviors()
    {
        // Enable the sky.
        ModContent.GetInstance<EternalGardenSky>().ShouldBeActive = NamelessDeitySky.HeavenlyBackgroundIntensity <= 0f;

        // Keep it perpetually night time if Nameless is not present.
        if (NamelessDeityBoss.Myself is null)
        {
            Main.dayTime = false;
            Main.time = 16200f;
        }

        // Keep the wind strong, so that the plants sway around.
        // This swiftly ceases if Nameless is present, as though nature is fearful of him.
        if (NamelessDeityBoss.Myself is null)
            Main.windSpeedTarget = Lerp(0.88f, 1.32f, AperiodicSin(Main.GameUpdateCount * 0.02f) * 0.5f + 0.5f);
        else
            Main.windSpeedTarget = 0f;
        Main.windSpeedCurrent = Lerp(Main.windSpeedCurrent, Main.windSpeedTarget, 0.03f);

        // Create a god ray at the center of the garden if Nameless isn't present.
        int godRayID = ModContent.ProjectileType<GodRayVisual>();
        if (Main.netMode != NetmodeID.MultiplayerClient && (NamelessDeityBoss.Myself is null || ModContent.GetInstance<EndCreditsScene>().IsActive) && !AnyProjectiles(godRayID))
        {
            WasInSubworldLastUpdateFrame = true;

            Vector2 centerOfWorld = new Point(Main.maxTilesX / 2, EternalGardenWorldGen.SurfaceTilePoint).ToWorldCoordinates() + Vector2.UnitY * 320f;
            NewProjectileBetter(new EntitySource_WorldEvent(), centerOfWorld, Vector2.Zero, godRayID, 0, 0f);
        }

        // No.
        Main.bloodMoon = false;
        Main.pumpkinMoon = false;
        Main.snowMoon = false;

        // Check if anyone is in the center of the garden for the purpose of determining if the time-in-center timer should increment.
        // This does not apply if Nameless is present.
        // This also does not apply if the end credits are playing.
        AnyoneTryingToSummonNameless = false;
        if (NamelessDeityBoss.Myself is null && !ModContent.GetInstance<EndCreditsScene>().IsActive)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;

                bool playerHasMoonscreen = false;
                for (int j = 0; j < 58; j++)
                {
                    if (p.inventory[j].stack >= 1 && p.inventory[j].ModItem is Moonscreen moonscreen && moonscreen.Opened)
                    {
                        playerHasMoonscreen = true;
                        break;
                    }
                }

                if (playerHasMoonscreen)
                    continue;

                if (Distance(p.Center.X, Main.maxTilesX * 8f) <= (EternalGardenWorldGen.TotalFlatTilesAtCenter + 8f) * 16f)
                {
                    AnyoneTryingToSummonNameless = true;
                    break;
                }
            }
        }

        // Play a special sound if the player enters the center.
        if (TimeSpentInLight == 2 && AnyoneTryingToSummonNameless)
        {
            StandingInCenterSound = SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.StandingInLight with { Volume = 1.15f }, null, sound =>
            {
                if (!AnyoneTryingToSummonNameless)
                    sound.Volume = Saturate(sound.Volume - 0.05f);

                return WasInSubworldLastUpdateFrame;
            });
        }

        if (SoundEngine.TryGetActiveSound(StandingInCenterSound, out ActiveSound? sound))
            sound.Callback?.Invoke(sound);

        // Disallow player movement shortly after the sound plays to force the feeling of suspense.
        if (TimeSpentInLight == 150 && AnyoneTryingToSummonNameless)
            BlockerSystem.Start(true, false, () => TimeSpentInLight >= 150);

        // Spawn Nameless if a player has spent a sufficient quantity of time in the center of the garden.
        TimeSpentInLight = Utils.Clamp(TimeSpentInLight + AnyoneTryingToSummonNameless.ToDirectionInt(), 0, 600);
        if (Main.netMode != NetmodeID.MultiplayerClient && TimeSpentInLight >= NamelessDeitySummonDelayInCenter && NamelessDeityBoss.Myself is null)
        {
            NPC.NewNPC(new EntitySource_WorldEvent(), Main.maxTilesX * 8, EternalGardenWorldGen.SurfaceTilePoint * 16 - 800, ModContent.NPCType<NamelessDeityBoss>(), 1);
            TimeSpentInLight = 0;
        }

        // Check if Soulyn should be spawned.
        bool canSpawnSolyn = NamelessDeityBoss.Myself is null && BossDownedSaveSystem.HasDefeated<NamelessDeityBoss>() && !WorldVersionSystem.PreAvatarUpdateWorld;
        if (Main.netMode != NetmodeID.MultiplayerClient && canSpawnSolyn && !NPC.AnyNPCs(ModContent.NPCType<Solyn>()))
            NPC.NewNPC(new EntitySource_WorldEvent(), Main.maxTilesX * 8 - 950, EternalGardenWorldGen.SurfaceTilePoint * 16 - 300, ModContent.NPCType<Solyn>(), 1);

        // Make the music dissipate in accordance with how long the player has been in the center.
        MusicVolumeManipulationSystem.MuffleFactor = 1f - InverseLerp(NamelessDeitySummonDelayInCenter * 0.1f, NamelessDeitySummonDelayInCenter * 0.5f, TimeSpentInLight);

        // Disable typical weather things.
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            if (Sandstorm.Happening)
                Sandstorm.StopSandstorm();
            Main.StopRain();
            Main.StopSlimeRain();
        }

        [JITWhenModsEnabled(CalamityCompatibility.ModName)]
        static void DisableMusicEvent()
        {
            // No interludes. This is an endgame area.
            MusicEventSystem.CurrentEvent = null;
            MusicEventSystem.TrackStart = null;
        }

        if (CalamityCompatibility.Enabled)
            DisableMusicEvent();
    }

    public static void StartPlayerFadeInEffects()
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player p = Main.player[i];
            if (!p.active || p.dead)
                continue;

            p.GetValueRef<float>(PlayerLightFadeInInterpolantName).Value = 0.001f;
            if (Main.myPlayer == i)
            {
                BlockerSystem.Start(true, false, () => p.GetValueRef<float>(PlayerLightFadeInInterpolantName).Value < 0.7f);
                SoundEngine.PlaySound(GennedAssets.Sounds.Common.TeleportIn with { Volume = 0.65f, MaxInstances = 5, PitchVariance = 0.16f });
            }
        }
    }

    public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
    {
        if (!WasInSubworldLastUpdateFrame && NamelessDeitySky.SkyIntensityOverride <= 0f)
            return;

        tileColor = new(81, 119, 135);

        if (WasInSubworldLastUpdateFrame)
            backgroundColor = new(4, 6, 14);

        // Make the background brighter the closer the camera is to the center of the world.
        float centerOfWorld = Main.maxTilesX * 8f;
        float distanceToCenterOfWorld = Distance(Main.screenPosition.X + Main.screenWidth * 0.5f, centerOfWorld);
        float brightnessInterpolant = InverseLerp(3200f, 1400f, distanceToCenterOfWorld);
        if (WasInSubworldLastUpdateFrame)
            backgroundColor = Color.Lerp(backgroundColor, Color.LightCoral, brightnessInterpolant * 0.27f);
        tileColor = Color.Lerp(tileColor, Color.LightPink, brightnessInterpolant * 0.4f);

        if (NamelessDeityFormPresetRegistry.UsingLucillePreset)
            tileColor = Color.Lerp(tileColor, Color.Red, 0.5f);
        else
            tileColor = Color.Lerp(tileColor, new Color(150, 210, 255), 0.7f);

        // Make everything bright if Nameless is present.
        tileColor = Color.Lerp(tileColor, Color.White, MathF.Max(NamelessDeitySky.HeavenlyBackgroundIntensity, NamelessDeitySky.SkyIntensityOverride));
    }
}
