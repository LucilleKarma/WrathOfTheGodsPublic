using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.Pets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.FormPresets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering.RenderSteps;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.CrossCompatibility.Inbound.Infernum;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.World.Subworlds;
using NoxusBoss.Core.World.WorldSaving;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

[AutoloadBossHead]
public partial class NamelessDeityBoss : ModNPC, IInfernumBossBarSupport, IBossDowned
{
    #region Custom Types and Enumerations

    public enum WingMotionState
    {
        Flap,
        RiseUpward,
    }

    public enum NamelessAIType
    {
        // Spawn animation behaviors.
        Awaken,
        OpenScreenTear,
        IntroScreamAnimation,

        // Magic attacks.
        ConjureExplodingStars,
        ArcingEyeStarbursts,
        RealityTearDaggers,
        SuperCosmicLaserbeam,

        // Fire attacks.
        SunBlenderBeams,
        PerpendicularPortalLaserbeams,

        // General cosmic attacks.
        CrushStarIntoQuasar,
        InwardStarPatternedExplosions,
        BackgroundStarJumpscares,
        SwordConstellation,
        MomentOfCreation,

        // Spiritual attacks.
        PsychedelicFeatherStrikes,

        // Reality manipulation attacks.
        VergilScreenSlices,
        RealityTearPunches,
        ClockConstellation,
        DarknessWithLightSlashes,

        // Phase transitions.
        EnterPhase2,
        EnterPhase2_AttackPlayer,
        EnterPhase2_Return,
        EnterPhase3,

        // Death animation variants.
        DeathAnimation,
        DeathAnimation_GFB,

        // GFB exclusive things.
        RodOfHarmonyRant,
        PlayerHurtAvatarRant,
        Glock,

        // State that occurs during the Avatar's fight, where Nameless saves the player.
        SavePlayerFromAvatar,

        // Credits state.
        EndCreditsScene,

        // Intermediate states.
        Teleport,
        ResetCycle,

        // Useful count constant.
        Count
    }

    #endregion Custom Types and Enumerations

    #region Fields and Properties

    public bool AutomaticallyRegisterDeathGlobally => true;

    /// <summary>
    /// Private backing field for <see cref="Myself"/>.
    /// </summary>
    private static NPC? myself;

    /// <summary>
    /// The sound slot that handles Nameless' idle ambience.
    /// </summary>
    public SlotId IdleSoundSlot;

    /// <summary>
    /// The render composite that Nameless uses.
    /// </summary>
    public NamelessDeityRenderComposite RenderComposite;

    /// <summary>
    /// A list of all of Nameless' hands. Hands may be created and destroyed at will via <see cref="ConjureHandsAtPosition(Vector2, Vector2, bool)"/> and <see cref="DestroyAllHands(bool)"/>.
    /// </summary>
    public List<NamelessDeityHand> Hands => RenderComposite.Find<ArmsStep>().Hands;

    /// <summary>
    /// The current phase represented as an integer. Zero corresponds to Phase 1, One corresponds to Phase 2, etc.
    /// </summary>
    public int CurrentPhase
    {
        get;
        set;
    }

    /// <summary>
    /// The length of the current fight. This represents the amount of frames it's been since Nameless spawned in.
    /// </summary>
    public int FightTimer
    {
        get;
        set;
    }

    /// <summary>
    /// Nameless' current universal difficulty.
    /// </summary>
    /// 
    /// <remarks>
    /// It is expected that this value will only be changed when Nameless changes his AI state, so that this can gracefully affect timers.
    /// </remarks>
    public float DifficultyFactor
    {
        get;
        private set;
    } = 1f;

    /// <summary>
    /// Nameless' Z position value. This is critical for attacks where he enters the background.<br></br>
    /// When in the background, his aesthetic is overall darker and smaller, to sell the illusion.
    /// </summary>
    public float ZPosition
    {
        get;
        set;
    }

    /// <summary>
    /// A general purpose hover offset vector for attacks.
    /// </summary>
    public Vector2 GeneralHoverOffset
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the music should slow down and stop.
    /// </summary>
    public bool StopMusic
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Nameless should draw behind tiles in its layering due to his current <see cref="ZPosition"/>.
    /// </summary>
    public bool ShouldDrawBehindTiles => ZPosition >= 0.2f;

    /// <summary>
    /// The intensity of afterimages relative to Nameless' speed.
    /// </summary>
    public float AfterimageOpacityFactor => Utils.Remap(NPC.velocity.Length(), 13f, 37.5f, 0.7f, 1.05f);

    /// <summary>
    /// The chance for an afterimage to spawn for a given frame.
    /// </summary>
    public float AfterimageSpawnChance => Saturate(InverseLerp(23f, 50f, NPC.velocity.Length()) * 0.6f + Clamp(ZPosition - 0.4f, 0f, 50f) * 0.3f);

    /// <summary>
    /// Nameless' life ratio as a 0-1 value. Used notably for phase transition triggers.
    /// </summary>
    public float LifeRatio => Saturate(NPC.life / (float)NPC.lifeMax);

    /// <summary>
    /// Nameless' current target. Can be either a player or the Avatar's second phase form.
    /// </summary>
    public NPCAimedTarget Target => NPC.GetTargetData();

    /// <summary>
    /// A shorthand for the <see cref="Target"/>'s horizontal direction. Useful for "Teleport behind the target" effects.
    /// </summary>
    public int TargetDirection
    {
        get
        {
            if (NPC.HasPlayerTarget)
                return Main.player[NPC.target].direction;

            NPC target = Main.npc[NPC.TranslatedTargetIndex];
            return target.velocity.X.NonZeroSign();
        }
    }

    /// <summary>
    /// The wind loop sound instance.
    /// </summary>
    public LoopedSoundInstance WindSoundLoopInstance
    {
        get;
        private set;
    }

    /// <summary>
    /// Nameless' scale, relative to the effects of <see cref="TeleportVisualsInterpolant"/>. Should be used for most contexts instead of <see cref="NPC.scale"/>.
    /// </summary>
    public Vector2 TeleportVisualsAdjustedScale
    {
        get
        {
            float maxStretchFactor = 1.4f;
            Vector2 scale = Vector2.One * NPC.scale;
            if (TeleportVisualsInterpolant > 0f && TeleportVisualsInterpolant < 1f)
            {
                // 1. Horizontal stretch.
                if (TeleportVisualsInterpolant <= 0.25f)
                {
                    float localInterpolant = InverseLerp(0f, 0.25f, TeleportVisualsInterpolant);
                    scale.X *= Lerp(1f, maxStretchFactor, Convert01To010(localInterpolant));
                    scale.Y *= Lerp(1f, 0.1f, Pow(localInterpolant, 2f));
                }

                // 2. Vertical collapse.
                else if (TeleportVisualsInterpolant <= 0.5f)
                {
                    float localInterpolant = Pow(InverseLerp(0.5f, 0.25f, TeleportVisualsInterpolant), 1.02f);
                    scale.X = localInterpolant;
                    scale.Y = localInterpolant * 0.1f;
                }

                // 3. Return to normal scale, use vertical overshoot at the end.
                else
                {
                    float localInterpolant = InverseLerp(0.5f, 0.92f, TeleportVisualsInterpolant);

                    // 1.594424 = 1 / sin(1.96)^6, acting as a correction factor to ensure that the final scale in the sinusoidal overshoot is one.
                    float verticalScaleOvershot = Pow(Sin(localInterpolant * 1.96f), 6f) * 1.594424f;
                    scale.X = localInterpolant;
                    scale.Y = verticalScaleOvershot;
                }
            }

            // AWESOME!
            if (NamelessDeityFormPresetRegistry.UsingAmmyanPreset)
                scale.X *= 2.4763f;

            return scale;
        }
    }

    /// <summary>
    /// The current AI state Nameless is using. This uses the <see cref="StateMachine"/> under the hood.
    /// </summary>
    public NamelessAIType CurrentState
    {
        get
        {
            // Add the relevant phase cycle if it has been exhausted, to ensure that Nameless' attacks are cyclic.
            if (StateMachine.StateStack is not null && (StateMachine?.StateStack?.Count ?? 1) <= 0)
                StateMachine?.StateStack.Push(StateMachine.StateRegistry[NamelessAIType.ResetCycle]);

            return StateMachine?.CurrentState?.Identifier ?? NamelessAIType.Awaken;
        }
    }

    /// <summary>
    /// A 0-1 interpolant for teleport effects. This affects Nameless' scale.<br></br>
    /// In the 0 to 0.5 range, Nameless fades out, with the expectation that at 0.5 the teleport will happen.<br></br>
    /// In the 0.5 to 1 range, Nameless fade in again, with the expectation that the teleport has concluded.
    /// </summary>
    public ref float TeleportVisualsInterpolant => ref NPC.localAI[0];

    /// <summary>
    /// The flap animation type for Nameless' large wings. Should be <see cref="WingMotionState.Flap"/> for most contexts.
    /// </summary>
    public WingMotionState WingsMotionState
    {
        get => (WingMotionState)NPC.localAI[3];
        set => NPC.localAI[3] = (int)value;
    }

    /// <summary>
    /// The world position for the censored eye flower.
    /// </summary>
    public Vector2 EyePosition => NPC.Center - Vector2.UnitY.RotatedBy(NPC.rotation) * TeleportVisualsAdjustedScale.Y * 226f;

    /// <summary>
    /// The ideal world position for the censor overlay.
    /// </summary>
    public Vector2 IdealCensorPosition => EyePosition + Vector2.UnitY.RotatedBy(NPC.rotation) * TeleportVisualsAdjustedScale.Y * 120f;

    /// <summary>
    /// A shorthand accessor for the Nameless Deity NPC. Returns null if not currently present.
    /// </summary>
    public static NPC? Myself
    {
        get
        {
            if (Main.gameMenu)
                return myself = null;

            if (myself is not null && !myself.active)
                return null;
            if (myself is not null && myself.type != ModContent.NPCType<NamelessDeityBoss>())
                return myself = null;

            return myself;
        }
        internal set => myself = value;
    }

    /// <summary>
    /// A static reference to Nameless' current <see cref="DifficultyFactor"/>.
    /// </summary>
    public static float Myself_DifficultyFactor
    {
        get
        {
            if (Myself is not null)
                return Myself.As<NamelessDeityBoss>().DifficultyFactor;

            return 1f;
        }
    }

    /// <summary>
    /// Nameless' current state as defined by his <see cref="Myself"/> instance.
    /// </summary>
    public static NamelessAIType? Myself_CurrentState => Myself?.As<NamelessDeityBoss>().CurrentState;

    /// <summary>
    /// The amount of damage starburst projectiles (Such as <see cref="Starburst"/>) do.
    /// </summary>
    public static int StarburstDamage => GetAIInt("StarburstDamage");

    /// <summary>
    /// The amount of damage supernova plasma (aka the little moving things during the quasar attack) do.
    /// </summary>
    public static int SupernovaPlasmaDamage => GetAIInt("SupernovaPlasmaDamage");

    /// <summary>
    /// The amount of damage exploding stars do.
    /// </summary>
    public static int ExplodingStarDamage => GetAIInt("ExplodingStarDamage");

    /// <summary>
    /// The amount of damage light daggers do.
    /// </summary>
    public static int DaggerDamage => GetAIInt("DaggerDamage");

    /// <summary>
    /// The amount of damage screen slices do.
    /// </summary>
    public static int ScreenSliceDamage => GetAIInt("ScreenSliceDamage");

    /// <summary>
    /// The amount of damage primordial stardust (the stuff created from Nameless' fans during the perpendicular laser attack) does.
    /// </summary>
    public static int PrimordialStardustDamage => GetAIInt("PrimordialStardustDamage");

    /// <summary>
    /// The amount of damage sun laserbeams (the ones from the big held-in-hand sun attack) do.
    /// </summary>
    public static int SunLaserDamage => GetAIInt("SunLaserDamage");

    /// <summary>
    /// The amount of damage light slashes (the ones during the scary background attack) do.
    /// </summary>
    public static int LightSlashDamage => GetAIInt("LightSlashDamage");

    /// <summary>
    /// The amount of damage portal laserbeams do.
    /// </summary>
    public static int PortalLaserbeamDamage => GetAIInt("PrimordialStardustDamage");

    /// <summary>
    /// The amount of damage the sword constellation does.
    /// </summary>
    public static int SwordConstellationDamage => GetAIInt("SwordConstellation_BaseSlashAnimationTime");

    /// <summary>
    /// The amount of damage falling galaxies (the ones from the moment of creation attack) do.
    /// </summary>
    public static int GalaxyDamage => GetAIInt("GalaxyDamage");

    /// <summary>
    /// The amount of damage the flying quasar does.
    /// </summary>
    public static int QuasarDamage => GetAIInt("QuasarDamage");

    /// <summary>
    /// The amount of damage the super cosmic laserbeam does.<br></br>
    /// <i>Hits several times per second, resulting in a shredding effect. This is why the damage values are abnormally low.</i>
    /// </summary>
    public static int CosmicLaserbeamDamage => GetAIInt("CosmicLaserbeamDamage");

    /// <summary>
    /// The ideal duration of a Nameless Deity fight in frames.<br></br>
    /// This value is used in the Timed DR system to make Nameless more resilient if the player is killing him unusually quickly.
    /// </summary>
    public static int IdealFightDuration => MinutesToFrames(GetAIFloat("IdealFightDurationMinutes"));

    /// <summary>
    /// How strongly the effects of timed DR should be applied.
    /// </summary>
    public static float TimedDRSharpness => GetAIFloat("TimedDRSharpness");

    /// <summary>
    /// The maximum amount of damage reduction Nameless should have as a result of timed DR. A value of 0.75 for example would correspond to 75% damage reduction.<br></br>
    /// This mechanic exists to make balance more uniform (Yes, it is evil. Blame the absurdity of shadowspec tier balancing).
    /// </summary>
    public static float MaxTimedDRDamageReduction => GetAIFloat("MaxTimedDRDamageReduction");

    /// <summary>
    /// The life ratio at which Nameless may transition to his second phase.<br></br>
    /// Where reasonable, this tries to wait for the current attack to conclude before transitioning.
    /// </summary>
    public static float Phase2LifeRatio => GetAIFloat("Phase2LifeRatio");

    /// <summary>
    /// The life ratio at which Nameless may transition to his third phase.<br></br>
    /// Unlike the second phase, this happens immediately and is a much more sudden effect.
    /// </summary>
    public static float Phase3LifeRatio => GetAIFloat("Phase3LifeRatio");

    /// <summary>
    /// A list of all phase threshold life ratios. This is used by Infernum's boss bar, assuming it's enabled.
    /// </summary>
    public IEnumerable<float> PhaseThresholdLifeRatios
    {
        get
        {
            yield return Phase2LifeRatio;
            yield return Phase3LifeRatio;
        }
    }

    /// <summary>
    /// The amount of damage reduction Nameless should have by default in a 0-1 range. A value of 0.25 for example would correspond to 25% damage reduction.
    /// </summary>
    public static float DefaultDR => GetAIFloat("DefaultDR");

    /// <summary>
    /// How big Nameless should be in general. Be careful with this number, as it will affect things like design impact and hitboxes.
    /// </summary>
    public static float DefaultScaleFactor => GetAIFloat("DefaultScaleFactor");

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/NamelessDeity", Name);

    #endregion Fields and Properties

    #region AI
    public override void AI()
    {
        // Pick a target if the current one is invalid.
        bool invalidTargetIndex = Target.Invalid;
        if (invalidTargetIndex)
            TargetClosest();

        if (!NPC.WithinRange(Target.Center, 4600f))
            TargetClosest();

        if (AvatarOfEmptiness.Myself is not null)
            NPC.target = AvatarOfEmptiness.Myself.target;

        // Leave if the target is gone.
        if (Target.Invalid)
        {
            SoundMufflingSystem.MuffleFactor = 1f;
            MusicVolumeManipulationSystem.MuffleFactor = 1f;
            NamelessDeitySky.KaleidoscopeInterpolant = 0f;
            NamelessDeitySky.HeavenlyBackgroundIntensity = 0f;
            NamelessDeitySky.SeamScale = 0f;
            NPC.active = false;
        }

        // Grant players infinite flight and the boss effects buff.
        foreach (Player player in Main.ActivePlayers)
        {
            if (player.dead)
                continue;

            player.wingTime = player.wingTimeMax;
            CalamityCompatibility.GrantInfiniteCalFlight(player);
            CalamityCompatibility.GrantBossEffectsBuff(player);
        }

        NPCID.Sets.TakesDamageFromHostilesWithoutBeingFriendly[Type] = NPC.HasNPCTarget;
        if (NPC.HasNPCTarget && NPC.position.Y <= 1600f)
            NPC.position.Y = Main.maxTilesY * 16f - 250f;
        if (NPC.HasNPCTarget && NPC.position.Y >= Main.maxTilesY * 16f - 900f)
            NPC.position.Y = 2300f;

        if (NPC.HasNPCTarget && NPC.position.X <= 400f)
            NPC.position.X = Main.maxTilesX * 16f - 900f;
        if (NPC.HasNPCTarget && NPC.position.X >= Main.maxTilesX * 16f + 400f)
            NPC.position.X = 900f;

        // Take damage from NPCs and projectiles if fighting the Avatar.
        NPC.takenDamageMultiplier = NPC.HasNPCTarget ? 5f : 1f;

        // Do not despawn.
        NPC.timeLeft = 7200;

        Myself = NPC;
        PerformPreUpdateResets();
        DisableWeatherAmbience();
        DisableLifeStealEffects();

        StateMachine.PerformBehaviors();
        StateMachine.PerformStateTransitionCheck();

        if (Main.zenithWorld)
            DisallowAlicornOnAStickCheese();

        if (Main.rand.NextBool(AfterimageSpawnChance) && CurrentState != NamelessAIType.SavePlayerFromAvatar)
            SpawnAfterimage();

        // Handle mumble sounds.
        if (MumbleTimer >= 1)
        {
            MumbleTimer++;

            float mumbleCompletion = MumbleTimer / 45f;
            if (mumbleCompletion >= 1f)
                MumbleTimer = 0;

            // Play the sound.
            if (MumbleTimer == 16)
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.Mumble with { Volume = 0.89f }, Target.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 7.5f);
            }
        }

        // Disable damage when invisible or in the end credits state.
        if (NPC.Opacity <= 0.35f || CurrentState == NamelessAIType.EndCreditsScene)
        {
            NPC.ShowNameOnHover = false;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
        }

        // Get rid of all falling stars. Their noises completely ruin the ambience.
        // active = false must be used over Kill because the Kill method causes them to drop their fallen star items.
        IEnumerable<Projectile> fallingStars = AllProjectilesByID(ProjectileID.FallingStar);
        foreach (Projectile star in fallingStars)
            star.active = false;

        // Make the censor intentionally move in a bit of a "choppy" way, where it tries to stick to the ideal position, but only if it's far
        // enough away.
        // As a failsafe, it sticks perfectly if Nameless is moving really quickly so that it doesn't gain too large of a one-frame delay. Don't want to be
        // accidentally revealing what's behind there, after all.
        if (NPC.position.Distance(NPC.oldPosition) >= 76f)
            CensorPosition = IdealCensorPosition;
        else if (!CensorPosition.WithinRange(IdealCensorPosition, 34f) || ZPosition >= 2f)
            CensorPosition = IdealCensorPosition;

        // Increment timers.
        AITimer++;
        if (CurrentState != NamelessAIType.Awaken && CurrentState != NamelessAIType.OpenScreenTear)
        {
            FightTimer++;

            // Let's make him TRULY endless.
            if (FightTimer >= int.MaxValue - 10)
            {
                FightTimer -= 8000000;
                NPC.netUpdate = true;
            }
        }

        // Update keyboard shader effects.
        NamelessDeityKeyboardShader.EyeBrightness = NPC.Opacity;

        // Perform Z position visual effects.
        PerformZPositionEffects();

        // Update the idle sounds.
        UpdateIdleSound();

        // Update the render composite.
        RenderComposite.Update();

        if (TestOfResolveSystem.IsActive)
            HandleTestOfResolve();

        // Make it night time. This does not apply if time is being manipulated by the clock.
        if (!AnyProjectiles(ModContent.ProjectileType<ClockConstellation>()) && EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
        {
            Main.dayTime = false;
            Main.time = Lerp((float)Main.time, 16200f, 0.14f);
        }

        // Hold HP in place while waiting for phase 2 or death.
        if (!WaitingForDeathAnimation && CurrentPhase == 0 && NPC.life <= NPC.lifeMax * Phase2LifeRatio)
            WaitingForPhase2Transition = true;
        if (WaitingForDeathAnimation)
        {
            NamelessDeitySky.SeamScale = 0f;
            NPC.life = 1;
            NPC.immortal = true;
        }
        if (WaitingForPhase2Transition)
        {
            NPC.life = (int)Round(NPC.lifeMax * Phase2LifeRatio);
            NPC.immortal = true;
        }

        // Update hands.
        foreach (NamelessDeityHand hand in Hands)
            hand.Update();

        // Rotate based on horizontal speed by default.
        // As an exception, Nameless spins wildly if the player's name is smh as a """dev preset""".
        if (NamelessDeityFormPresetRegistry.UsingSmhPreset)
            NPC.rotation += NPC.velocity.X.NonZeroSign() * 0.6f;
        else
        {
            float idealRotation = Clamp(NPC.velocity.X * 0.0033f, -0.16f, 0.16f);
            NPC.rotation = NPC.rotation.AngleLerp(idealRotation, 0.3f).AngleTowards(idealRotation, 0.045f);
        }

        // Disable the sulph sea background, since it has a tendency to overlay the boss background.
        SulphSeaSkyDisabler.DisableSulphSeaSky = true;
    }

    /// <summary>
    /// Disables weather ambience that does not suit the fight, such as sandstorms and rain.
    /// </summary>
    public static void DisableWeatherAmbience()
    {
        // Say NO to weather that destroys the ambience!
        // They cannot take away your gameplay aesthetic without your consent.
        Main.StopRain();
        for (int i = 0; i < Main.maxRain; i++)
            Main.rain[i].active = false;

        if (Main.netMode != NetmodeID.Server)
        {
            Sandstorm.StopSandstorm();

            if (Main.netMode != NetmodeID.Server)
                Filters.Scene["Graveyard"].Deactivate();
        }
    }

    /// <summary>
    /// Disables life steal effects for all plays, such as that performed by the vampire knives.
    /// </summary>
    public static void DisableLifeStealEffects()
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player p = Main.player[i];
            if (!p.active || p.dead)
                continue;

            p.moonLeech = true;
        }
    }

    /// <summary>
    /// Handles stuff involving the Test of Resolve, including making Nameless completely invincible.
    /// </summary>
    public void HandleTestOfResolve()
    {
        NPC.life = NPC.lifeMax = 400000000;
        NPC.immortal = true;
        NamelessDeitySky.KaleidoscopeInterpolant *= 0.95f;
        Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/ARIA BEYOND THE SHINING FIRMAMENT");

        // Super, super gradually let the player get their HP back.
        int healBackRate = GetAIInt("TestOfResolve_HealBackRate");
        if (FightTimer % healBackRate == healBackRate - 1)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                Core.DataStructures.Referenced<int> hitRef = player.GetValueRef<int>(TestOfResolveSystem.RemainingHitsVariableName);
                if (hitRef >= 1)
                    hitRef.Value = Utils.Clamp(hitRef + 1, 0, 20);
            }
        }

        // Give everyone a lotus if they somehow made it far enough.
        int lotusEarnDelay = GetAIInt("TestOfResolve_MinutesNeededToEarnLotus") * 3600;
        if (FightTimer == lotusEarnDelay)
        {
            foreach (Player player in Main.ActivePlayers)
                Item.NewItem(new EntitySource_WorldEvent(), player.Center, ModContent.ItemType<Erilucyxwyn>());
        }
    }

    /// <summary>
    /// Checks to see if the target player has Calamity's Alicorn on a Stick item in use, and turns it into a unicorn horn if so.
    /// </summary>
    /// <remarks>
    /// This feature is meant to be be exclusive to the Getfixedboi world seed.
    /// </remarks>
    public void DisallowAlicornOnAStickCheese()
    {
        if (ModReferences.Calamity is null || !NPC.HasPlayerTarget || !Target.Center.WithinRange(NPC.Center, TeleportVisualsAdjustedScale.X * 420f))
            return;

        Item item = Main.player[NPC.TranslatedTargetIndex].HeldMouseItem();
        int cheatItemID = ModReferences.Calamity.Find<ModItem>("AlicornonaStick").Type;
        if (item.type == cheatItemID && Myself is not null)
            item.SetDefaults(ItemID.UnicornHorn);
    }

    /// <summary>
    /// Summons an afterimage near Nameless' current position.
    /// </summary>
    public void SpawnAfterimage()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        Vector2 afterimageSpawnPosition = NPC.Center + Main.rand.NextVector2Circular(20f, 20f);
        Vector2 afterimageVelocity = Main.rand.NextVector2Circular(9f, 9f) * AfterimageOpacityFactor;
        NPC.NewProjectileBetter(NPC.GetSource_FromAI(), afterimageSpawnPosition, afterimageVelocity, ModContent.ProjectileType<NamelessDeityAfterimage>(), 0, 0f, -1, MathF.Max(TeleportVisualsAdjustedScale.X, TeleportVisualsAdjustedScale.Y), NPC.rotation);
    }

    /// <summary>
    /// Resets various things pertaining to the fight state prior to behavior updates.
    /// </summary>
    /// <remarks>
    /// This serves as a means of ensuring that changes to the fight state are gracefully reset if something suddenly changes, while affording the ability to make changes during updates.<br></br>
    /// As a result, this alleviates behaviors AI states from the burden of having to assume that they may terminate at any time and must account for that to ensure that the state is reset.
    /// </remarks>
    public void PerformPreUpdateResets()
    {
        NPC.damage = NPC.defDamage;
        NPC.defense = NPC.defDefense;
        NPC.dontTakeDamage = false;
        NPC.immortal = false;
        NPC.ShowNameOnHover = true;
        ModReferences.Calamity?.Call("SetDRSpecific", NPC, DefaultDR);
        TeleportVisualsInterpolant = 0f;
        HandsShouldInheritOpacity = true;
        DrawHandsSeparateFromRT = false;
        StopMusic = false;
        CosmicBackgroundSystem.StarZoomIncrement = 0f;

        bool darkeningWasAboveZero = RelativeDarkening > 0f;
        RelativeDarkening = Saturate(RelativeDarkening - 0.01f);
        if (darkeningWasAboveZero)
            NamelessDeitySky.HeavenlyBackgroundIntensity = 1f - RelativeDarkening;
    }

    /// <summary>
    /// Finds a new target for Nameless.<br></br>
    /// This performs the standard player distance search check, but gives the Avatar's second form absolute targeting if present.
    /// </summary>
    public void TargetClosest()
    {
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
        {
            NPC.target = AvatarOfEmptiness.Myself.target;
            return;
        }

        NPCUtils.TargetSearchResults targetSearchResults = NPCUtils.SearchForTarget(NPC, NPCUtils.TargetSearchFlag.NPCs | NPCUtils.TargetSearchFlag.Players, _ => AvatarOfEmptiness.Myself is null, AvatarSearchCheck);
        if (!targetSearchResults.FoundTarget)
            return;

        // Check for players. Prioritize the Avatar if he's present.
        int targetIndex = targetSearchResults.NearestTargetIndex;
        if (targetSearchResults.FoundNPC)
            targetIndex = targetSearchResults.NearestNPCIndex + 300;

        NPC.target = targetSearchResults.NearestTargetIndex;
        NPC.targetRect = targetSearchResults.NearestTargetHitbox;
    }

    /// <summary>
    /// Search filter for the Avatar of Emptiness.
    /// </summary>
    /// <param name="npc">The NPC to check.</param>
    public static bool AvatarSearchCheck(NPC npc)
    {
        return npc.type == ModContent.NPCType<AvatarOfEmptiness>() && npc.Opacity > 0f && !npc.immortal && !npc.dontTakeDamage && npc.active && !npc.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing;
    }

    #endregion AI
}
