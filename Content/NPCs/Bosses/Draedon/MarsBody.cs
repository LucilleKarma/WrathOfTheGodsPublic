using System.Runtime.CompilerServices;

using CalamityMod.NPCs.ExoMechs.Thanatos;
using CalamityMod.Sounds;

using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Luminance.Core.Sounds;

using Microsoft.Xna.Framework;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Autoloaders;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Data;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using NoxusBoss.Core.World.WorldSaving;

using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

[AutoloadBossHead]
public partial class MarsBody : ModNPC, IBossDowned
{
    public enum MarsAIType
    {
        SpawnAnimation,
        TeachPlayerAboutTeamAttack,

        FirstPhaseMissileRailgunCombo,

        EnergyWeaveSequence,
        PostEnergyWeaveSequenceStun,

        BrutalBarrage,
        BrutalBarrageGrindForcefield,
        ElectricCageBlasts,
        UpgradedMissileRailgunCombo,
        Malfunction,

        CarvedLaserbeam,

        VulnerableUntilDeath,

        EndCreditsScene,

        ResetCycle,

        Count
    }

    #region Fields and Properties

    private static NPC? myself;

    public bool AutomaticallyRegisterDeathGlobally => false;

    /// <summary>
    /// Mars' dedicated timer for use when he's in the process of despawning.
    /// </summary>
    public int DespawnTimer
    {
        get;
        set;
    }

    /// <summary>
    /// The timer for Solyn and the player's team attack
    /// </summary>
    public int SolynPlayerTeamAttackTimer 
    { 
        get; 
        set; 
    }

    /// <summary>
    /// The identifier of Mars for the purposes of render targets.
    /// </summary>
    public int RenderTargetIdentifier => NPC.IsABestiaryIconDummy ? -1 : NPC.whoAmI;

    /// <summary>
    /// Whether Solyn and the player can do team attacks or not.
    /// </summary>
    public bool SolynAndPlayerCanDoTeamAttack
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the chainsaw part of the energy cannon is active or not.
    /// </summary>
    public bool EnergyCannonChainsawActive
    {
        get;
        set;
    }

    /// <summary>
    /// How activated the chainsaw currently is. This determines the windup of the chainsaw loop sound and the speed of the chainsaw whirr animation.
    /// </summary>
    public float ChainsawActivationInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// A general-purpose timer which determines how far along Mars' visual chainsaw effect is.
    /// </summary>
    public float ChainsawVisualsTimer
    {
        get;
        set;
    }

    /// <summary>
    /// Mars' previous state as dictated by the <see cref="MarsAIType.ResetCycle"/> function.
    /// </summary>
    public MarsAIType PreviousState
    {
        get;
        set;
    }

    /// <summary>
    /// The angle of Mars' railgun cannon.
    /// </summary>
    public float RailgunCannonAngle
    {
        get;
        set;
    }

    /// <summary>
    /// The angle of Mars' energy cannon.
    /// </summary>
    public float EnergyCannonAngle
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the line telegraph from Mars' railgun.
    /// </summary>
    public float RailgunCannonTelegraphOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// How much Mars' health bar colors should be biased to white.
    /// </summary>
    public float HealthBarImpactVisualInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The angular direction of the beam fired by Solyn and the player.
    /// </summary>
    public float TagTeamBeamDirection
    {
        get;
        set;
    }

    /// <summary>
    /// The effective position of Mars' left hand.
    /// </summary>
    public Vector2 LeftHandPosition
    {
        get;
        private set;
    }

    /// <summary>
    /// The velocity of Mars' left hand.
    /// </summary>
    public Vector2 LeftHandVelocity
    {
        get;
        private set;
    }

    /// <summary>
    /// The velocity of Mars' right hand.
    /// </summary>
    public Vector2 RightHandVelocity
    {
        get;
        private set;
    }

    /// <summary>
    /// The previous effective position of Mars' right hand.
    /// </summary>
    public Vector2 RightHandPosition
    {
        get;
        private set;
    }

    /// <summary>
    /// The previous effective position of Mars' left hand.
    /// </summary>
    public Vector2 OldLeftHandPosition
    {
        get;
        private set;
    }

    /// <summary>
    /// The effective position of Mars' right hand.
    /// </summary>
    public Vector2 OldRightHandPosition
    {
        get;
        private set;
    }

    /// <summary>
    /// The ideal position of Mars' left hand.
    /// </summary>
    ///
    /// <remarks>
    /// Importantly, this is not the <i>actual</i> posistion of the hand, since the hand's true position is limited by the arm's reach.
    /// For the hand's true position, refer to <see cref="LeftHandPosition"/>.
    /// </remarks>
    public Vector2 IdealLeftHandPosition
    {
        get;
        set;
    }

    /// <summary>
    /// The ideal position of Mars' right hand.
    /// </summary>
    ///
    /// <remarks>
    /// Importantly, this is not the <i>actual</i> posistion of the hand, since the hand's true position is limited by the arm's reach.
    /// For the hand's true position, refer to <see cref="RightHandPosition"/>.
    /// </remarks>
    public Vector2 IdealRightHandPosition
    {
        get;
        set;
    }

    /// <summary>
    /// The position of Mars' core.
    /// </summary>
    public Vector2 CorePosition => NPC.Center + Vector2.UnitY.RotatedBy(NPC.rotation) * NPC.scale * 22f;

    /// <summary>
    /// Mars' current target.
    /// </summary>
    public Player Target => Main.player[NPC.target];

    /// <summary>
    /// The sound perpetually played by Mars' engine.
    /// </summary>
    public LoopedSoundInstance EngineLoop
    {
        get;
        set;
    }

    /// <summary>
    /// The sound perpetually played by Mars when his chainsaw is in use.
    /// </summary>
    public LoopedSoundInstance ChainsawLoop
    {
        get;
        set;
    }

    /// <summary>
    /// The action that Solyn should perform.
    /// </summary>
    public Action<BattleSolyn> SolynAction
    {
        get;
        set;
    }

    /// <summary>
    /// The action that Solyn's sentient star should perform.
    /// </summary>
    public Action<SolynSentientStar> SolynStarAction
    {
        get;
        set;
    }

    /// <summary>
    /// Mars' phase 2 life ratio threshold.
    /// </summary>
    public static float Phase2LifeRatio => GetAIFloat("Phase2LifeRatio");

    /// <summary>
    /// Mars' phase 3 life ratio threshold.
    /// </summary>
    public static float Phase3LifeRatio => GetAIFloat("Phase3LifeRatio");

    /// <summary>
    /// Mars' phase 4 life ratio threshold.
    /// </summary>
    public static float Phase4LifeRatio => GetAIFloat("Phase4LifeRatio");

    /// <summary>
    /// The amount of gravity imposed on Mars' ropes and wires.
    /// </summary>
    public static float RopeGravity => 5f;

    /// <summary>
    /// The amount of DR applied to Mars when hit by normal weaponry.
    /// </summary>
    public static float NormalWeaponDamageReduction => GetAIFloat("NormalWeaponDamageReduction");

    /// <summary>
    /// Mars' <see cref="NPC"/> instance. Returns <see langword="null"/> if he is not present.
    /// </summary>
    public static NPC? Myself
    {
        get
        {
            if (Main.gameMenu)
                return myself = null;

            if (myself is not null && !myself.active)
                return null;
            if (myself is not null && myself.type != ModContent.NPCType<MarsBody>())
                return myself = null;

            return myself;
        }
        private set => myself = value;
    }

    /// <summary>
    /// The amount of damage that the tag team beam from Solyn and the player does to Mars.
    /// </summary>
    public static int TagTeamBeamBaseDamage => GetAIInt("TagTeamBeamBaseDamage");

    /// <summary>
    /// The path to the JSON file that contains values pertaining to Mars' battle.
    /// </summary>
    public const string AIValuesPath = "Content/NPCs/Bosses/Draedon/MarsAIValues.json";

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Draedon/Mars", Name);

    #endregion Fields and Properties

    #region Initialization

    public override void Load()
    {
        string contentPath = GetAssetPath("Content/Items/Placeable/MusicBoxes", "MarsMusicBox");
        string musicPath = "Assets/Sounds/Music/Mars";
        MusicBoxAutoloader.Create(Mod, contentPath, musicPath, out _, out _);
    }

    public override void SetStaticDefaults()
    {
        NPCID.Sets.MustAlwaysDraw[Type] = true;
        NPCID.Sets.TrailingMode[NPC.type] = 3;
        NPCID.Sets.TrailCacheLength[NPC.type] = 50;
        NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers()
        {
            Scale = 0.5f,
            PortraitScale = 0.6f,
            PortraitPositionYOverride = 2f,
            Position = Vector2.UnitY * 16f
        };
        NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);

        // Apply miracleblight immunities.
        CalamityCompatibility.MakeImmuneToMiracleblight(NPC);

        if (Main.netMode != NetmodeID.Server)
            InitializeTargets();

        // Allow the Mars' chainsaw to optionally do contact damage.
        On_NPC.GetMeleeCollisionData += ExpandEffectiveHitboxForHands;

        PlayerDataManager.CanUseItemEvent += DisableItemUsageWhenChargingUp;
    }

    private void ExpandEffectiveHitboxForHands(On_NPC.orig_GetMeleeCollisionData orig, Rectangle victimHitbox, int enemyIndex, ref int specialHitSetter, ref float damageMultiplier, ref Rectangle npcRect)
    {
        orig(victimHitbox, enemyIndex, ref specialHitSetter, ref damageMultiplier, ref npcRect);

        // See the big comment in CanHitPlayer.
        if (Main.npc[enemyIndex].type == Type && Main.npc[enemyIndex].As<MarsBody>().EnergyCannonChainsawActive)
            npcRect.Inflate(4000, 4000);
    }

    private static bool DisableItemUsageWhenChargingUp(PlayerDataManager p, Item item)
    {
        if (Myself is null)
            return true;

        if (GameSceneSlowdownSystem.SlowdownInterpolant >= 0.3f)
            return false;

        MarsBody mars = Myself.As<MarsBody>();
        if (!mars.SolynAndPlayerCanDoTeamAttack)
            return true;

        if (p.Player != mars.Target) 
            return true;

        return mars.SolynPlayerTeamAttackTimer <= 0 && p.Player.ownedProjectileCounts[ModContent.ProjectileType<SolynTagTeamBeam>()] <= 0;
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 50f;

        // Set up hitbox data.
        NPC.width = 240;
        NPC.height = 210;

        // Define HP and damage.
        // Yes, I know. ApplyDifficultyAndPlayerScaling sets damage too.
        // If you don't do it here the game refuses to let the boss do contact damage ever.
        NPC.lifeMax = GetAIInt("TotalHP");
        NPC.damage = GetAIInt("ContactDamage");
        if (CalamityCompatibility.Enabled)
            CalamityCompatibility.SetLifeMaxByMode_ApplyCalBossHPBoost(NPC);

        // Do not use any default AI states.
        NPC.aiStyle = -1;
        AIType = -1;

        // Use 100% knockback resistance.
        NPC.knockBackResist = 0f;

        // Be immune to lava.
        NPC.lavaImmune = true;

        // Disable tile collision and gravity.
        NPC.noGravity = true;
        NPC.noTileCollide = true;

        // Act as a boss.
        NPC.boss = true;
        NPC.BossBar = ModContent.GetInstance<MarsBossBar>();

        // Set the music.
        Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/Mars");
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        // Define stats.
        NPC.lifeMax = (int)Round(NPC.lifeMax * bossAdjustment / (Main.masterMode ? 3f : 2f));
        NPC.damage = GetAIInt("ContactDamage");
        NPC.defense = GetAIInt("Defense");
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
        {
            new FlavorTextBestiaryInfoElement($"Mods.{Mod.Name}.Bestiary.{Name}")
        });
    }

    #endregion Initialization

    #region Network Code

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((int)PreviousState);
        writer.Write(RailgunCannonAngle);
        writer.Write(SolynAndPlayerCanDoTeamAttack);
        writer.Write(EnergyCannonChainsawActive);
        writer.Write(DespawnTimer);
        writer.WriteVector2(IdealLeftHandPosition);
        writer.WriteVector2(IdealRightHandPosition);
        writer.WriteVector2(LeftHandVelocity);
        writer.WriteVector2(RightHandVelocity);
        writer.Write(EnergyWeaveSequence_SolynDashPlayer);

        // Write state data.
        var stateStack = (StateMachine?.StateStack ?? new Stack<EntityAIState<MarsAIType>>()).ToList();
        writer.Write(stateStack.Count);
        for (int i = stateStack.Count - 1; i >= 0; i--)
        {
            writer.Write(stateStack[i].Time);
            writer.Write((byte)stateStack[i].Identifier);
        }
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        AITimer--;

        PreviousState = (MarsAIType)reader.ReadInt32();
        RailgunCannonAngle = reader.ReadSingle();
        SolynAndPlayerCanDoTeamAttack = reader.ReadBoolean();
        EnergyCannonChainsawActive = reader.ReadBoolean();
        DespawnTimer = reader.ReadInt32();
        IdealLeftHandPosition = reader.ReadVector2();
        IdealRightHandPosition = reader.ReadVector2();
        LeftHandVelocity = reader.ReadVector2();
        RightHandVelocity = reader.ReadVector2();
        EnergyWeaveSequence_SolynDashPlayer = reader.ReadInt32();

        // Read state data.
        int stateStackCount = reader.ReadInt32();
        for (int i = 0; i < stateStackCount; i++)
        {
            int time = reader.ReadInt32();
            byte stateType = reader.ReadByte();
            StateMachine.StateStack.Push(StateMachine.StateRegistry[(MarsAIType)stateType]);
            StateMachine.StateRegistry[(MarsAIType)stateType].Time = time;
        }
    }

    #endregion Network Code

    #region AI
    public override void AI()
    {
        // Set the global NPC instance.
        Myself = NPC;

        PerformStateSafetyCheck();

        // Reset things every frame.
        bool chainsawWasActive = EnergyCannonChainsawActive;
        NPC.dontTakeDamage = CurrentState == MarsAIType.EndCreditsScene;
        NPC.gfxOffY = 0f;
        NPC.damage = 0;
        NPC.hide = false;
        NPC.ShowNameOnHover = NPC.Opacity >= 0.35f;
        EnergyCannonChainsawActive = false;
        HealthBarImpactVisualInterpolant = Saturate(HealthBarImpactVisualInterpolant - 0.075f);
        AltCannonVisualInterpolant = Saturate(AltCannonVisualInterpolant - 0.015f);
        SilhouetteInterpolant = Saturate(SilhouetteInterpolant - 0.015f);
        RailgunCannonTelegraphOpacity = 0f;

        float oldAltCannonVisualInterpolant = AltCannonVisualInterpolant;

        // Initialize wires.
        if (Wires is null || Wires.Length <= 0)
            Wires = GenerateWires();

        // Make Solyn's default action staying near the player.
        SolynAction = StandardSolynBehavior_FlyNearPlayer;

        // Update all of Mars' ropes.
        UpdateRopes();

        // Despawn if everyone's dead.
        if (PerformDespawnCheck())
        {
            DoBehavior_Despawn();
            IdealLeftHandPosition += LeftHandVelocity;
            IdealRightHandPosition += RightHandVelocity;
            PerformArmIKCalculations();

            DespawnTimer++;
            return;
        }

        // Reset the despawn timer if Mars is not despawning.
        DespawnTimer = 0;

        StateMachine.PerformBehaviors();
        StateMachine.PerformStateTransitionCheck();

        if (!ModContent.GetInstance<EndCreditsScene>().IsActive)
            UpdateLoopSounds();

        // Update chainsaw information.
        ChainsawActivationInterpolant = Saturate(ChainsawActivationInterpolant + EnergyCannonChainsawActive.ToDirectionInt() * NPC.Opacity * 0.011f);
        ChainsawVisualsTimer += (1f - GameSceneSlowdownSystem.SlowdownInterpolant) * ChainsawActivationInterpolant / 60f;
        if (EnergyCannonChainsawActive != chainsawWasActive && !ModContent.GetInstance<EndCreditsScene>().IsActive)
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.ChainsawArmSwitch, RightHandPosition);

        // Update Mars' arms.
        IdealLeftHandPosition += LeftHandVelocity;
        IdealRightHandPosition += RightHandVelocity;
        PerformArmIKCalculations();

        // Disallow natural despawns.
        NPC.timeLeft = 7200;

        // Ensure Mars' HP doesn't go beyond a minimum threshold if he isn't supposed to be killed yet.
        PerformStateSafetyCheck();
        int minHP = (int)(NPC.lifeMax * 0.05f);
        if (NPC.life < minHP && CurrentState != MarsAIType.VulnerableUntilDeath && CurrentState != MarsAIType.CarvedLaserbeam)
            NPC.life = minHP;

        // Let Solyn enter the battle.
        if (!ModContent.GetInstance<EndCreditsScene>().IsActive)
            BattleSolyn.SummonSolynForBattle(NPC.GetSource_FromAI(), Target.Center, BattleSolyn.SolynAIType.FightMars);

        int starID = ModContent.ProjectileType<SolynSentientStar>();
        foreach (Projectile star in Main.ActiveProjectiles)
        {
            if (star.type == starID)
                SolynStarAction?.Invoke(star.As<SolynSentientStar>());
        }

        if (oldAltCannonVisualInterpolant != AltCannonVisualInterpolant &&
            (oldAltCannonVisualInterpolant == 0f || oldAltCannonVisualInterpolant == 1f))
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.SingularityCannonSwap, NPC.Center);
        }

        BurnMarks.ForEach(m => m.Time++);
        BurnMarks.RemoveAll(m => m.Time >= m.Lifetime);

        // Increment the AI timer.
        PerformStateSafetyCheck();
        AITimer++;
    }

    /// <summary>
    /// Resets all Solyn/player tag team timers to 0.
    /// </summary>
    public void ResetSolynPlayerTeamAttackTimers()
    {
        if (SolynPlayerTeamAttackTimer != 0)
        {
            SolynPlayerTeamAttackTimer = 0;
            PacketManager.SendPacket<MarsBeamChargePacket>();
        }
    }

    /// <summary>
    /// Creates and updates Mars' looped sounds.
    /// </summary>
    public void UpdateLoopSounds()
    {
        EngineLoop ??= LoopedSoundManager.CreateNew(GennedAssets.Sounds.Mars.EngineLoop, () => !NPC.active);
        EngineLoop.Update(NPC.Center, s =>
        {
            float bodySpeed = (NPC.position - NPC.oldPosition).Length();
            float maxArmSpeed = MathF.Max(LeftHandVelocity.Length(), RightHandVelocity.Length()) - bodySpeed;
            s.Volume = (InverseLerp(7f, 45f, maxArmSpeed).Squared() * 1.3f + 0.65f) * ThrusterStrength;
        });

        if ((ChainsawLoop is null || ChainsawLoop.HasBeenStopped) && EnergyCannonChainsawActive)
        {
            ChainsawLoop = LoopedSoundManager.CreateNew(GennedAssets.Sounds.Mars.ChainsawLoop, () => !NPC.active || !EnergyCannonChainsawActive);
            ChainsawLoop.Update(RightHandPosition, s => s.Volume = 0.0001f);

            // Turboscuffed solution to ensure that the sound doesn't get to play at full volume on the first frame of the chainsaw activating.
            if (ChainsawActivationInterpolant <= 0.1f)
                ChainsawLoop.Stop();
        }
        else if (EnergyCannonChainsawActive)
            ChainsawLoop?.Restart();

        ChainsawLoop?.Update(RightHandPosition, s =>
        {
            float idealVolume = NPC.Opacity.Cubed() * (1f - SilhouetteInterpolant).Squared() * InverseLerp(800f, 400f, RightHandPosition.Distance(Main.LocalPlayer.Center)) * ChainsawActivationInterpolant;

            s.Pitch = Convert01To010(ChainsawActivationInterpolant) * 0.25f + ChainsawActivationInterpolant * 0.25f;
            s.Volume = Lerp(s.Volume, idealVolume * 0.6f, 0.14f) * (1f - GameSceneSlowdownSystem.SlowdownInterpolant);
        });
    }

    /// <summary>
    /// Moves Mars' arms to a given relative offset.
    /// </summary>
    ///
    /// <remarks>
    /// Offsets are automatically rotated and scaled based on Mars' transform in this method.
    /// </remarks>
    ///
    /// <param name="leftArmOffset">The relative ideal offset for the left arm.</param>
    /// <param name="rightArmOffset">The relative ideal offset for the right arm.</param>
    /// <param name="movementSharpness">The sharpness of the hand movement.</param>
    /// <param name="movementSmoothness">The smoothness of the hand movement.</param>
    public void MoveArmsTowards(Vector2 leftArmOffset, Vector2 rightArmOffset, float movementSharpness = 0.25f, float movementSmoothness = 0.75f)
    {
        Vector2 leftArmDestination = NPC.Center + leftArmOffset.RotatedBy(NPC.rotation) * NPC.scale;
        Vector2 rightArmDestination = NPC.Center + rightArmOffset.RotatedBy(NPC.rotation) * NPC.scale;
        LeftHandVelocity = Vector2.Lerp(LeftHandVelocity, (leftArmDestination - IdealLeftHandPosition) * movementSharpness, 1f - movementSmoothness);
        RightHandVelocity = Vector2.Lerp(RightHandVelocity, (rightArmDestination - IdealRightHandPosition) * movementSharpness, 1f - movementSmoothness);
    }

    /// <summary>
    /// Calculates the position of Mars' arms.
    /// </summary>
    public void PerformArmIKCalculations()
    {
        // Store the old positions.
        OldLeftHandPosition = LeftHandPosition;
        OldRightHandPosition = RightHandPosition;

        leftElbowPosition = CalculateElbowPosition(LeftShoulderPosition, IdealLeftHandPosition, ArmLength, ForearmLength, false);
        rightElbowPosition = CalculateElbowPosition(RightShoulderPosition, IdealRightHandPosition, ArmLength, ForearmLength, false);

        LeftHandPosition = leftElbowPosition + leftElbowPosition.SafeDirectionTo(IdealLeftHandPosition) * ForearmLength;
        RightHandPosition = rightElbowPosition + rightElbowPosition.SafeDirectionTo(IdealRightHandPosition) * ForearmLength;
    }

    /// <summary>
    /// Teleports Mars to a new position.
    /// </summary>
    /// <param name="teleportPosition">The position Mars should be teleported to.</param>
    public void ImmediateTeleportTo(Vector2 teleportPosition)
    {
        Vector2 teleportOffset = teleportPosition - NPC.Center;
        NPC.Center += teleportOffset;
        NPC.netUpdate = true;

        TopLeftRedPipe.OffsetAllPoints(teleportOffset);
        BottomLeftRedPipe.OffsetAllPoints(teleportOffset);
        TopRightRedPipe.OffsetAllPoints(teleportOffset);
        BottomRightRedPipe.OffsetAllPoints(teleportOffset);

        IdealLeftHandPosition += teleportOffset;
        IdealRightHandPosition += teleportOffset;
        LeftHandVelocity = Vector2.Zero;
        RightHandVelocity = Vector2.Zero;
    }

    /// <summary>
    /// Checks if every player is dead or not, and returns whether Mars should despawn as a result.
    /// </summary>
    /// <returns></returns>
    public bool PerformDespawnCheck()
    {
        if (NPC.target <= -1 || NPC.target >= Main.maxPlayers || Target.dead || !Target.active)
        {
            NPC.TargetClosest();
            if (NPC.target <= -1 || NPC.target >= Main.maxPlayers || Target.dead || !Target.active)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Provides all players with a forcefield.
    /// </summary>
    public void ProvideForcefieldsToPlayers()
    {
        foreach (Player player in Main.ActivePlayers)
        {
            if (Main.myPlayer == player.whoAmI && player.ownedProjectileCounts[ModContent.ProjectileType<DirectionalSolynForcefield>()] <= 0)
                NewProjectileBetter(NPC.GetSource_FromAI(), player.Center, player.SafeDirectionTo(Main.MouseWorld), ModContent.ProjectileType<DirectionalSolynForcefield>(), 0, 0f, player.whoAmI);
        }
    }

    /// <summary>
    /// Makes Mars despawn.
    /// </summary>
    public void DoBehavior_Despawn()
    {
        int flyOffDelay = 90;
        float thrusterWeakenInterpolant = SmoothStep(0f, 1f, InverseLerp(0f, flyOffDelay, DespawnTimer));
        float thrusterStrengthenInterpolant = InverseLerp(0f, 15f, DespawnTimer - flyOffDelay).Squared();
        ThrusterStrength = Lerp(1f, 0.35f, thrusterWeakenInterpolant) + thrusterStrengthenInterpolant * 1.67f;

        NPC.dontTakeDamage = true;
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f, 0.3f);

        if (thrusterStrengthenInterpolant <= 0f)
            NPC.velocity *= 0.9f;
        else
        {
            NPC.velocity.Y -= thrusterStrengthenInterpolant.Cubed() * 10f;

            if (DespawnTimer == flyOffDelay + 1)
                RadialScreenShoveSystem.Start(NPC.Center, 120);
            if (DespawnTimer == flyOffDelay + 8)
            {
                ScreenShakeSystem.StartShake(30f, TwoPi, null, 0.9f);
                GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 2f, 20);
            }

            if (!NPC.WithinRange(Target.Center, 3100f))
                NPC.active = false;
        }

        MoveArmsTowards(new(-30f, 120f), new(30f, 120f));
    }

    /// <summary>
    /// Updates all of Mars' ropes.
    /// </summary>
    public void UpdateRopes()
    {
        TopLeftRedPipe.Update(NPC, RopeGravity, true);
        BottomLeftRedPipe.Update(NPC, RopeGravity, true);
        TopRightRedPipe.Update(NPC, RopeGravity, true);
        BottomRightRedPipe.Update(NPC, RopeGravity, true);

        SmallLeftPipe.Rope.IdealRopeLength = NPC.scale * 60f;
        SmallRightPipe.Rope.IdealRopeLength = NPC.scale * 60f;

        Vector2 smallLeftPipeEnd = Vector2.Lerp(LeftShoulderPosition, leftElbowPosition, 0.35f) - Vector2.UnitY * 6f;
        SmallLeftPipe.StartingOffset = new Vector2(-90f, -8f);
        SmallLeftPipe.EndingOffset = (smallLeftPipeEnd - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale;

        Vector2 smallRightPipeEnd = Vector2.Lerp(RightShoulderPosition, rightElbowPosition, 0.35f) - Vector2.UnitY * 6f;
        SmallRightPipe.StartingOffset = new Vector2(90f, -8f);
        SmallRightPipe.EndingOffset = (smallRightPipeEnd - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale;

        BigLeftPipe.Rope.IdealRopeLength = NPC.scale * 70f;
        BigRightPipe.Rope.IdealRopeLength = NPC.scale * 70f;

        Vector2 bigLeftPipeEnd = Vector2.Lerp(LeftShoulderPosition, leftElbowPosition, 0.35f) - Vector2.UnitY * 6f;
        BigLeftPipe.StartingOffset = new Vector2(-90f, -16f);
        BigLeftPipe.EndingOffset = (bigLeftPipeEnd - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale;

        Vector2 bigRightPipeEnd = Vector2.Lerp(RightShoulderPosition, rightElbowPosition, 0.35f) - Vector2.UnitY * 6f;
        BigRightPipe.StartingOffset = new Vector2(90f, -16f);
        BigRightPipe.EndingOffset = (bigRightPipeEnd - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale;

        BigLeftPipe.Update(NPC, RopeGravity, true);
        BigRightPipe.Update(NPC, RopeGravity, true);
        SmallLeftPipe.Update(NPC, RopeGravity, true);
        SmallRightPipe.Update(NPC, RopeGravity, true);

        LeftShoulderPipeA.StartingOffset = (Vector2.Lerp(leftElbowPosition, LeftHandPosition, 0.05f) - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale + Vector2.UnitY * 10f;
        LeftShoulderPipeA.EndingOffset = (LeftHandPosition - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale;
        LeftShoulderPipeB.StartingOffset = LeftShoulderPipeA.StartingOffset - Vector2.UnitY * 6f;
        LeftShoulderPipeB.EndingOffset = LeftShoulderPipeA.EndingOffset - Vector2.UnitY * 12f;

        RightShoulderPipeA.StartingOffset = (Vector2.Lerp(rightElbowPosition, RightHandPosition, 0.05f) - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale + Vector2.UnitY * 10f;
        RightShoulderPipeA.EndingOffset = (RightHandPosition - NPC.Center).RotatedBy(-NPC.rotation) / NPC.scale;
        RightShoulderPipeB.StartingOffset = RightShoulderPipeA.StartingOffset - Vector2.UnitY * 6f;
        RightShoulderPipeB.EndingOffset = RightShoulderPipeA.EndingOffset - Vector2.UnitY * 12f;

        float ropeStretchiness = 2.95f;
        LeftShoulderPipeA.Rope.IdealRopeLength = LeftShoulderPipeA.StartingOffset.Length() * NPC.scale * ropeStretchiness;
        LeftShoulderPipeB.Rope.IdealRopeLength = LeftShoulderPipeB.StartingOffset.Length() * NPC.scale * ropeStretchiness;
        RightShoulderPipeA.Rope.IdealRopeLength = RightShoulderPipeA.StartingOffset.Length() * NPC.scale * ropeStretchiness;
        RightShoulderPipeB.Rope.IdealRopeLength = RightShoulderPipeB.StartingOffset.Length() * NPC.scale * ropeStretchiness;

        LeftShoulderPipeA.Update(NPC, RopeGravity, true);
        LeftShoulderPipeB.Update(NPC, RopeGravity, true);
        RightShoulderPipeA.Update(NPC, RopeGravity, true);
        RightShoulderPipeB.Update(NPC, RopeGravity, true);
    }

    /// <summary>
    /// Performs a state machine safety check, ensuring that a valid state is loaded into the machine.
    /// </summary>
    public void PerformStateSafetyCheck()
    {
        if (StateMachine is null || StateMachine.StateStack is null)
            return;

        // Add the relevant phase cycle if it has been exhausted, to ensure that Mars' attacks are cyclic.
        if (StateMachine.StateStack.Count <= 0)
            StateMachine.StateStack.Push(StateMachine.StateRegistry[MarsAIType.ResetCycle]);
    }

    /// <summary>
    /// Retrives a stored AI integer value with a given name.
    /// </summary>
    /// <param name="name">The value's named key.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetAIInt(string name) => (int)Round(GetAIFloat(name));

    /// <summary>
    /// Retrives a stored AI floating point value with a given name.
    /// </summary>
    /// <param name="name">The value's named key.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetAIFloat(string name) => LocalDataManager.Read<DifficultyValue<float>>(AIValuesPath)[name];

    #endregion AI

    #region Hit and Death Effects

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    public override void HitEffect(NPC.HitInfo hit)
    {
        if (NPC.soundDelay <= 0)
        {
            SoundEngine.PlaySound(CommonCalamitySounds.ExoHitSound, NPC.Center);
            NPC.soundDelay = 5;
        }
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    public void PlayHitSound(bool resisted)
    {
        if (NPC.soundDelay <= 0)
        {
            SoundStyle hitSound = resisted ? ThanatosHead.ThanatosHitSoundClosed : CommonCalamitySounds.ExoHitSound;
            if (NPC.life == NPC.lifeMax && CurrentState == MarsAIType.TeachPlayerAboutTeamAttack)
                hitSound = GennedAssets.Sounds.Mars.HitScream;

            SoundEngine.PlaySound(hitSound, NPC.Center);
            NPC.soundDelay = 5;
        }
    }

    public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        bool resisted = projectile.ModProjectile is not INotResistedByMars;
        if (CalamityCompatibility.Enabled)
            PlayHitSound(resisted);

        if (resisted)
            modifiers.FinalDamage *= 1f - NormalWeaponDamageReduction;
        if (projectile.DamageType == DamageClass.Summon)
            modifiers.SetMaxDamage(1);
    }

    public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers)
    {
        if (CalamityCompatibility.Enabled)
            PlayHitSound(true);

        modifiers.FinalDamage *= 1f - NormalWeaponDamageReduction;
    }

    public override bool CheckDead()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            NewProjectileBetter(NPC.GetSource_Death(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SyntheticSeedlingProjectile>(), 0, 0f);

        return true;
    }

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        // Ensure that the Avatar's contact damage adheres to the special boss-specific cooldown slot, to prevent things like lava cheese.
        cooldownSlot = ImmunityCooldownID.Bosses;

        return EnergyCannonChainsawActive && target.Hitbox.Intersects(Utils.CenteredRectangle(RightHandPosition, Vector2.One * NPC.scale * 96f));
    }

    #endregion Hit and Death Effects

    #region I love automatic despawning

    public override bool CheckActive() => false;

    #endregion I love automatic despawning
}
