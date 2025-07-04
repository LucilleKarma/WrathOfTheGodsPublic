using System.Reflection;
using CalamityMod.Buffs.StatDebuffs;
using Luminance.Common.DataStructures;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Biomes;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.FormPresets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.Autoloaders;
using NoxusBoss.Core.Bestiary;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Fixes;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.BackgroundManagement;
using NoxusBoss.Core.Graphics.UI;
using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.World.GameScenes.AvatarUniverseExploration;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using NoxusBoss.Core.World.Subworlds;
using NoxusBoss.Core.World.TileDisabling;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Events;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

[AutoloadBossHead]
public partial class AvatarOfEmptiness : ModNPC
{
    #region Custom Types and Enumerations
    public enum AvatarAIType
    {
        // Spawn animation behaviors.
        Awaken_RiftSizeIncrease,
        Awaken_LegEmergence,
        Awaken_ArmJutOut,
        Awaken_HeadEmergence,
        Awaken_Scream,

        // Portal based attacks.
        RubbleGravitySlam_ApplyExtremeGravity,
        RubbleGravitySlam_MoveRubble,

        // Blood Lily and other object-based attacks.
        DyingStarsWind,
        LilyStars_ChargePower,
        LilyStars_ReleaseStars,
        WorldSmash,

        // Dark attacks.
        Erasure,

        // Cryonic universe attacks.
        FrostScreenSmash_CreateFrost,
        FrostScreenSmash_EnterBackground,
        FrostScreenSmash_TwinkleTelegraphs,
        FrostScreenSmash_SmashForeground,
        WhirlingIceStorm,
        AbsoluteZeroOutburst,
        AbsoluteZeroOutburstPunishment,

        // Visceral universe attacks.
        BloodiedFountainBlasts,
        BloodiedFountainBlasts_DenseBurst,
        EnterBloodWhirlpool,
        ArmPortalStrikes,
        Unclog,
        AntishadowOnslaught,

        // Dark universe attacks.
        UniversalAnnihilation,
        RealityShatter_CreateAndWaitForTelegraph,
        RealityShatter_SlamDownward,
        RealityShatter_GroundCrack,
        RealityShatter_DimensionTwist,

        // Phase transition behaviors.
        Phase3TransitionScream,
        TravelThroughVortex,

        // GFB-specific behaviors.
        GivePlayerHeadpats,
        LeaveAfterBeingHit,

        // Intermediate states that are behavior agnostic.
        SendPlayerToMyUniverse,
        ReturnPlayersFromMyUniverse,
        BloodiedWeep,
        Teleport,
        TeleportAbovePlayer, // This must be defined separately for the purposes of allowing attack patterns to teleport without relying on the individual state behaviors to specify where the teleport should bring the Avatar.

        // Death animation behaviors.
        ParadiseReclaimed_SolynDialogue,
        ParadiseReclaimed_StaticChase,
        ParadiseReclaimed_SolynIsClaimed,
        ParadiseReclaimed_NamelessDispelsStatic,
        ParadiseReclaimed_NamelessReturnsEveryoneToOverworld,
        ParadiseReclaimed_FakeoutPhase,

        // The behavior used after everyone is killed.
        LeaveAfterPlayersAreDead,

        // End credits behavior.
        EndCreditsScene,

        // Technical behaviors.
        ResetCycle,

        // Used solely for iteration.
        Count
    }

    #endregion Custom Types and Enumerations

    #region Fields and Properties
    private static NPC? myself;

    private static readonly FieldInfo? currentBossBar = typeof(BigProgressBarSystem).GetField("_currentBar", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// The Avatar's current phase.
    /// </summary>
    public int CurrentPhase
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the Avatar is currently in phase 3, or a later phase.
    /// </summary>
    public bool Phase3 => CurrentPhase >= 1;

    /// <summary>
    /// Whether the Avatar is currently in phase 4, or a later phase.
    /// </summary>
    public bool Phase4 => CurrentPhase >= 2;

    /// <summary>
    /// How many attack patterns the Avatar has performed throughout the fight. Used to make every Nth pattern special during selection.
    /// </summary>
    public int AttackPatternCounter
    {
        get;
        set;
    }

    /// <summary>
    /// How many frames it's been since the Avatar's fight began.
    /// </summary>
    public int FightTimer
    {
        get;
        set;
    }

    /// <summary>
    /// How long the Avatar should wait before his distortion border should appear.
    /// </summary>
    public int BorderAppearanceDelay
    {
        get;
        set;
    }

    /// <summary>
    /// A designated timer used for the purposes of the rift's rotation.
    /// </summary>
    public float RiftRotationTimer
    {
        get;
        set;
    }

    /// <summary>
    /// A designated timer used for the purposes of the rift's rotation.
    /// </summary>
    public float RiftRotationSpeedInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The vanish interpolant of the rift behind the Avatar.
    /// </summary>
    public float RiftVanishInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// A 0-1 interpolant that dictates how many blood droplets should exist on the screen.
    /// </summary>
    public float BloodDropletOverlayInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the Avatar's front arms are detached.
    /// </summary>
    public bool FrontArmsAreDetached
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the Avatar is waiting for the current attack to finish so that he can enter his third phase.
    /// </summary>
    public bool WaitingForPhase3Transition
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the Avatar is waiting for the current attack to finish so that he can perform his death animation.
    /// </summary>
    public bool WaitingForDeathAnimation
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the Avatar needs to select a new set of dimension attack as soon as possible.<br></br>
    /// When true, this ensures that the next time the Avatar switches dimensions he will also choose an entirely new set.
    /// </summary>
    public bool NeedsToSelectNewDimensionAttacksSoon
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the battle is done and the Avatar is simply waiting for Solyn to perish before leaving.
    /// </summary>
    public bool BattleIsDone
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the Avatar should hide his boss HP bar.
    /// </summary>
    public bool HideBar
    {
        get;
        set;
    }

    /// <summary>
    /// The parsecs displayed on the compass meter in the Avatar's universe.
    /// </summary>
    public ulong HorizontalParsecs
    {
        get;
        set;
    }

    /// <summary>
    /// The parsecs displayed on the depth meter in the Avatar's universe.
    /// </summary>
    public ulong VerticalParsecs
    {
        get;
        set;
    }

    /// <summary>
    /// The Avatar's life to max life ratio.
    /// </summary>
    public float LifeRatio => NPC.life / (float)NPC.lifeMax;

    /// <summary>
    /// The direction of the Avatar's target. Used for cases of predictiveness.
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
    /// The Avatar's target.
    /// </summary>
    public NPCAimedTarget Target => NPC.GetTargetData();

    /// <summary>
    /// The looping sound that plays based on the Avatar's base shadowy wind.
    /// </summary>
    public LoopedSoundInstance? WindAmbienceLoop
    {
        get;
        set;
    }

    /// <summary>
    /// The looping sound that plays based on the Avatar's special dimension.
    /// </summary>
    public LoopedSoundInstance? DimensionAmbienceLoop
    {
        get;
        set;
    }

    /// <summary>
    /// The looping sound that plays when Nameless sends everyone back to the overworld.
    /// </summary>
    public LoopedSoundInstance? NamelessDeityVortexLoop
    {
        get;
        set;
    }

    /// <summary>
    /// The suction sound loop. Updated in accordance with the <see cref="SuckOpacity"/>.
    /// </summary>
    public LoopedSoundInstance? SuctionLoop
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether the Avatar was summoned via a summon item instead of by entering the second phase normally and thusly should spawn in a preset location.
    /// </summary>
    public bool ShouldTeleportAbovePlayer
    {
        get => NPC.ai[1] == 0f;
        set => NPC.ai[1] = value ? 0f : 1f;
    }

    /// <summary>
    /// The position of the Avatar's head.
    /// </summary>
    public Vector2 HeadPosition;

    /// <summary>
    /// The position of the Avatar's left front arm.
    /// </summary>
    public Vector2 LeftArmPosition;

    /// <summary>
    /// The position of the Avatar's right front arm.
    /// </summary>
    public Vector2 RightArmPosition;

    /// <summary>
    /// The desired center of the distortion screen shader. Defaults to the Avatar's center if <see langword="null"/>.
    /// </summary>
    public Vector2? DesiredDistortionCenterOverride
    {
        get;
        set;
    }

    /// <summary>
    /// The center of the distortion screen shader.
    /// </summary>
    public Vector2 DistortionCenter
    {
        get;
        set;
    }

    /// <summary>
    /// The position the target was in before the Avatar sent them to his universe.
    /// </summary>
    public Vector2 TargetPositionBeforeDimensionShift
    {
        get;
        set;
    }

    /// <summary>
    /// The position the target was at before the battle began.
    /// </summary>
    public Vector2 TargetPositionAtStart
    {
        get;
        set;
    }

    /// <summary>
    /// The Avatar's set of shadow arms.
    /// </summary>
    public List<AvatarShadowArm> ShadowArms
    {
        get;
        set;
    } = new(4);

    /// <summary>
    /// The current AI state the Avatar is using. This uses the <see cref="StateMachine"/> under the hood.
    /// </summary>
    public AvatarAIType CurrentState
    {
        get
        {
            PerformStateSafetyCheck();
            return StateMachine?.CurrentState?.Identifier ?? AvatarAIType.Awaken_RiftSizeIncrease;
        }
    }

    /// <summary>
    /// The first the Avatar universe attack that will be performed. Used when determining which dimension needs to be revealed during his vortex state.
    /// </summary>
    public AvatarAIType FirstDimensionAttack
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
    /// The Avatar's Z position. This corresponds to the Avatar's position in the background.
    /// </summary>
    public ref float ZPosition => ref NPC.ai[0];

    /// <summary>
    /// The Avatar's scale as a result of background scaling.
    /// </summary>
    public float ZPositionScale => (AvatarFormPresetRegistry.UsingDarknessFallsPreset ? 1.74f : 0.85f) / (ZPosition + 1f);

    /// <summary>
    /// The ideal distortion intensity for the screen shader.
    /// </summary>
    public ref float IdealDistortionIntensity => ref NPC.localAI[0];

    /// <summary>
    /// The distortion intensity for the screen shader. Approaches <see cref="IdealDistortionIntensity"/>.
    /// </summary>
    public ref float DistortionIntensity => ref NPC.localAI[1];

    /// <summary>
    /// The general volume factor for the current played ambient sound.
    /// </summary>
    public ref float AmbientSoundVolumeFactor => ref NPC.localAI[2];

    /// <summary>
    /// The maximum amount of damage reduction the Avatar should have as a result of timed DR. A value of 0.75 for example would correspond to 75% damage reduction.<br></br>
    /// This mechanic exists to make balance more uniform (Yes, it is evil. Blame the absurdity of shadowspec tier balancing).
    /// </summary>
    public static float MaxTimedDRDamageReduction => 0.81f;

    /// <summary>
    /// The ideal duration of a the Avatar fight in frames.<br></br>
    /// This value is used in the Timed DR system to make the Avatar more resilient if the player is killing him unusually quickly.
    /// </summary>
    public static float IdealFightDurationInMinutes => GetAIFloat("IdealFightDurationInMinutes");

    /// <summary>
    /// The Avatar's <see cref="NPC"/> instance. Returns <see langword="null"/> if the Avatar is not present.
    /// </summary>
    public static NPC? Myself
    {
        get
        {
            if (Main.gameMenu)
                return myself = null;

            if (myself is not null && !myself.active)
                return null;
            if (myself is not null && myself.type != ModContent.NPCType<AvatarOfEmptiness>())
                return myself = null;

            return myself;
        }
        private set => myself = value;
    }

    /// <summary>
    /// The amount of damage the Avatar's pale comets do.
    /// </summary>
    public static int CometDamage => GetAIInt("CometDamage");

    /// <summary>
    /// The amount of damage the Avatar's disgusting stars do.
    /// </summary>
    public static int DisgustingStarDamage => GetAIInt("DisgustingStarDamage");

    /// <summary>
    /// The amount of damage the Avatar's arm strikes do.
    /// </summary>
    public static int ArmStrikeDamage => GetAIInt("ArmStrikeDamage");

    /// <summary>
    /// The Avatar's default damage reduction percentage as a 0-1 value.
    /// </summary>
    public static float DefaultDR => GetAIFloat("DefaultDR_Avatar");

    /// <summary>
    /// The life ratio at which the Avatar transitions to his third phase.
    /// </summary>
    public static float Phase3LifeRatio => GetAIFloat("Phase3LifeRatio");

    /// <summary>
    /// The life ratio at which the Avatar transitions to his fourth phase.
    /// </summary>
    public static float Phase4LifeRatio => GetAIFloat("Phase4LifeRatio");

    /// <summary>
    /// The default hitbox size of the Avatar's rift.
    /// </summary>
    public static readonly Vector2 DefaultHitboxSize = new Vector2(601f, 697f);

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Avatar/SecondPhaseForm", Name);

    #endregion Fields and Properties

    #region Delegates and Events

    public delegate void OnHitPlayerDelegate(Player target, Player.HurtInfo hurtInfo);

    public event OnHitPlayerDelegate OnHitPlayerEvent;

    #endregion Delegates and Events

    #region Initialization

    /// <summary>
    /// The item ID of the Avatar's autoloaded mask.
    /// </summary>
    public static int MaskID
    {
        get;
        private set;
    }

    /// <summary>
    /// The item ID of the Avatar's autoloaded relic.
    /// </summary>
    public static int RelicID
    {
        get;
        private set;
    }

    /// <summary>
    /// The item ID of the Avatar's autoloaded treasure bag.
    /// </summary>
    public static int TreasureBagID
    {
        get;
        private set;
    }

    /// <summary>
    /// The text the compass should display when the player is in the Avatar's universe.
    /// </summary>
    public static string CompassTextInMyUniverse
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// The text the depth meter should display when the player is in the Avatar's universe.
    /// </summary>
    public static string DepthTextInMyUniverse
    {
        get;
        set;
    } = string.Empty;

    public override void Load()
    {
        // Autoload the mask.
        MaskID = MaskAutoloader.Create(Mod, GetAssetPath("Content/Items/Vanity", "NoxusMask"), false);

        // Autoload the relic.
        RelicAutoloader.Create(Mod, GetAssetPath("Content/Items/Placeable/Relics", "NoxusRelic"), out int relicID, out _);
        RelicID = relicID;

        // Autoload the treasure bag.
        TreasureBagID = TreasureBagAutoloader.Create(Mod, GetAssetPath("Content/Items/TreasureBags", "AvatarTreasureBag"), bag =>
        {
            bag.rare = ModContent.RarityType<AvatarRarity>();
        }, ModifyNPCBagLoot);

        // Autoload the music boxes.
        string contentPathP2 = GetAssetPath("Content/Items/Placeable/MusicBoxes", "AvatarOfEmptinessP2MusicBox");
        string musicPathP2 = "Assets/Sounds/Music/AvatarOfEmptinessP2";
        MusicBoxAutoloader.Create(Mod, contentPathP2, musicPathP2, out _, out _);

        string contentPathP3 = GetAssetPath("Content/Items/Placeable/MusicBoxes", "AvatarOfEmptinessP3MusicBox");
        string musicPathP3 = "Assets/Sounds/Music/AvatarOfEmptinessP3";
        MusicBoxAutoloader.Create(Mod, contentPathP3, musicPathP3, out _, out _);
    }

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MustAlwaysDraw[Type] = true;
        NPCID.Sets.TrailingMode[NPC.type] = 3;
        NPCID.Sets.TrailCacheLength[NPC.type] = 90;
        NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers()
        {
            Scale = 0.15f,
            PortraitScale = 0.3f
        };
        NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.UsesNewTargetting[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        EmptinessSprayer.NPCsToNotDelete[Type] = true;
        BestiaryBossOrderingPrioritySystem.Priority[Type] = 1;

        // Apply miracleblight immunities.
        CalamityCompatibility.MakeImmuneToMiracleblight(NPC);

        // Load textures.
        LoadTargets();

        // Fix the Avatar's hurtbox being way too big for the purposes of player rams.
        On_NPC.getRect += FixHurtbox;

        GlobalItemEventHandlers.CanUseItemEvent += DisableTeleportItems;
        PlayerDataManager.PostMiscUpdatesEvent += DisableSpaceEffects;

        MapStyleLockingSystem.RegisterConditionSet(1, true, () => AvatarOfEmptinessSky.Dimension?.AppliesSilhouetteShader ?? false);

        // Ensure that sound loops are immediately initialized, to address a frame-one lag spike issue that was present throughout testing.
        UpdateLoopingSounds();

        GraphicalUniverseImagerOptionManager.RegisterNew(new GraphicalUniverseImagerOption("Mods.NoxusBoss.UI.GraphicalUniverseImager.AvatarP2Background", true,
            GennedAssets.Textures.GraphicalUniverseImager.ShaderSource_Avatar, RenderGUIPortraitP2, (minDepth, maxDepth, settings) =>
            {
                AvatarOfEmptinessSky.InProximityOfMonolith = true;
                AvatarOfEmptinessSky.TimeSinceCloseToMonolith = 0;
            }));
        GraphicalUniverseImagerOptionManager.RegisterNew(new GraphicalUniverseImagerOption("Mods.NoxusBoss.UI.GraphicalUniverseImager.BloodWhirlpool", true,
            GennedAssets.Textures.GraphicalUniverseImager.ShaderSource_Avatar, RenderGUIPortraitBlood, (minDepth, maxDepth, settings) =>
            {
                Background.SetSpriteSortMode(SpriteSortMode.Immediate, Matrix.Identity);
                AvatarDimensionVariants.VisceralDimension.BackgroundDrawAction();
                Background.SetSpriteSortMode(SpriteSortMode.Deferred);
            }));
        GraphicalUniverseImagerOptionManager.RegisterNew(new GraphicalUniverseImagerOption("Mods.NoxusBoss.UI.GraphicalUniverseImager.CryonicStatic", true,
            GennedAssets.Textures.GraphicalUniverseImager.ShaderSource_Avatar, RenderGUIPortraitCryonic, (minDepth, maxDepth, settings) =>
            {
                Background.SetSpriteSortMode(SpriteSortMode.Immediate, Matrix.Identity);
                AvatarDimensionVariants.CryonicDimension.BackgroundDrawAction();
                Background.SetSpriteSortMode(SpriteSortMode.Deferred);
            }));

        CellPhoneInfoModificationSystem.WeatherReplacementTextEvent += (string originalText) =>
        {
            if (Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
            if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.CryonicDimension)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.CryogenicMaelstromText");
            if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.VisceralDimension)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.VisceralTyphoonText");
            if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.FogDimension || AvatarUniverseExplorationSystem.InAvatarUniverse)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.DyingWorldFogText");
            if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.DarkDimension)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.EmptinessWeatherText");
            if (Myself is not null && !Myself.As<AvatarOfEmptiness>().BattleIsDone)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.CrimsonDerechoText");

            return null;
        };
        CellPhoneInfoModificationSystem.FishingPowerReplacementTextEvent += (string originalText) =>
        {
            if (Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");

            return null;
        };
        CellPhoneInfoModificationSystem.MoonPhaseReplacementTextEvent += (string originalText) =>
        {
            if (Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");

            return null;
        };
        CellPhoneInfoModificationSystem.PlayerSpeedReplacementTextEvent += (string originalText) =>
        {
            if (Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
            if (TileDisablingSystem.TilesAreUninteractable && Myself is not null && Myself.As<AvatarOfEmptiness>().CurrentState == AvatarAIType.TravelThroughVortex)
            {
                float shownSpeed = Myself.As<AvatarOfEmptiness>().TravelThroughVortex_ShownPlayerSpeed;
                return Language.GetTextValue("GameUI.Speed", Math.Round(shownSpeed));
            }

            return null;
        };
        CellPhoneInfoModificationSystem.TreasureTilesReplacementTextEvent += (string originalText) =>
        {
            if (Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");

            return null;
        };
        CellPhoneInfoModificationSystem.PlayerXPositionReplacementTextEvent += (string originalText) =>
        {
            if (Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
            if (TileDisablingSystem.TilesAreUninteractable && Myself is not null)
                return CompassTextInMyUniverse;

            return null;
        };
        CellPhoneInfoModificationSystem.PlayerYPositionReplacementTextEvent += (string originalText) =>
        {
            if (Myself is not null && Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.ParadiseReclaimedInfoText");
            if (TileDisablingSystem.TilesAreUninteractable && Myself is not null)
                return DepthTextInMyUniverse;

            return null;
        };
    }

    private bool DisableTeleportItems(Item item, Player player)
    {
        if (AvatarOfEmptinessSky.Dimension is not null)
        {
            int itemID = item.type;
            bool mirror = itemID == ItemID.MagicMirror || itemID == ItemID.IceMirror || itemID == ItemID.CellPhone || itemID == ItemID.Shellphone || itemID == ItemID.ShellphoneSpawn;
            bool conch = itemID == ItemID.MagicConch || itemID == ItemID.DemonConch || itemID == ItemID.ShellphoneOcean || itemID == ItemID.ShellphoneHell;
            bool teleportPotion = itemID == ItemID.RecallPotion || itemID == ItemID.TeleportationPotion || itemID == ItemID.PotionOfReturn;
            if (mirror || conch || teleportPotion)
                return false;
        }

        return true;
    }

    private void DisableSpaceEffects(PlayerDataManager p)
    {
        float x = (Main.maxTilesX / 4200f).Squared();
        float spaceGravityMult = (float)((p.Player.position.Y / 16f - (60f + 10f * x)) / (Main.worldSurface / 6.0));
        bool inSpace = spaceGravityMult < 1f;

        if (Myself is not null && inSpace)
            p.Player.gravity = Player.defaultGravity;
    }

    private Rectangle FixHurtbox(On_NPC.orig_getRect orig, NPC self)
    {
        if (self.type == Type)
            return Utils.CenteredRectangle(self.Center, self.Size * self.scale * self.As<AvatarOfEmptiness>().ZPositionScale);

        return orig(self);
    }

    private static void RenderGUIPortraitP2(GraphicalUniverseImagerSettings settings)
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        Main.spriteBatch.Draw(WhitePixel, new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height), new Color(10, 10, 16));
        AvatarOfEmptinessSky.DrawBackground(intensityOverride: 1f);
        Main.spriteBatch.End();
    }

    private static void RenderGUIPortraitCryonic(GraphicalUniverseImagerSettings settings)
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        AvatarDimensionVariants.CryonicDimension.BackgroundDrawAction();
        Main.spriteBatch.End();
    }

    private static void RenderGUIPortraitBlood(GraphicalUniverseImagerSettings settings)
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        AvatarDimensionVariants.VisceralDimension.BackgroundDrawAction();
        Main.spriteBatch.End();
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 50f;
        NPC.damage = 400;
        NPC.width = (int)DefaultHitboxSize.X;
        NPC.height = (int)DefaultHitboxSize.Y;
        NPC.defense = 130;
        NPC.lifeMax = GetAIInt("DefaultHP_Avatar");
        if (CalamityCompatibility.Enabled)
            CalamityCompatibility.SetLifeMaxByMode_ApplyCalBossHPBoost(NPC);

        if (Main.expertMode)
        {
            NPC.damage = 600;

            // Undo vanilla's automatic Expert boosts.
            NPC.lifeMax /= 2;
            NPC.damage /= 2;
        }

        NPC.aiStyle = -1;
        AIType = -1;
        NPC.knockBackResist = 0f;
        NPC.canGhostHeal = false;
        NPC.boss = true;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.HitSound = null;
        NPC.DeathSound = null;
        NPC.value = Item.buyPrice(50, 0, 0, 0) / 5;
        NPC.netAlways = true;
        NPC.hide = true;
        NPC.BossBar = ModContent.GetInstance<AvatarBossBar>();
        CalamityCompatibility.MakeCalamityBossBarClose(NPC);
        LiquidDrawContents = new(40, 0.67f, completionRatio => ((1f - completionRatio) * 400f + 150f) * NPC.scale);

        SpawnModBiomes =
        [
            ModContent.GetInstance<DeadUniverseBiome>().Type
        ];

        Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/AvatarOfEmptinessP2");
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        // Remove the original NPCPortraitInfoElement instance, so that a new one with zero stars can be added.
        bestiaryEntry.Info.RemoveAll(i => i is NPCPortraitInfoElement);

        bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
        {
            new FlavorTextBestiaryInfoElement($"Mods.{Mod.Name}.Bestiary.{Name}"),
            new NPCPortraitInfoElement(0),
            new MoonLordPortraitBackgroundProviderBestiaryInfoElement()
        });
    }

    #endregion Initialization

    #region Networking

    public override void SendExtraAI(BinaryWriter writer)
    {
        // Send binary data.
        BitsByte b1 = new BitsByte()
        {
            [0] = FrontArmsAreDetached,
            [1] = WaitingForPhase3Transition,
            [2] = WaitingForDeathAnimation,
            [3] = HasSentPlayersThroughVortex,
            [4] = NeedsToSelectNewDimensionAttacksSoon
        };
        writer.Write(b1);

        // Write numerical data.
        writer.Write(FrostScreenSmashLineSlope);
        writer.Write(ParadiseReclaimed_LayerHeightBoost);
        writer.Write(ParadiseReclaimed_RenderStaticWallYPosition);
        writer.Write(ParadiseReclaimed_StaticPartInterpolant);
        writer.Write((int)PreviousState);
        writer.Write((int)FirstDimensionAttack);
        writer.Write(CurrentPhase);
        writer.Write(AttackPatternCounter);
        writer.Write(BorderAppearanceDelay);
        writer.Write(NPC.Opacity);
        writer.Write(NeckAppearInterpolant);
        writer.Write(LeftFrontArmScale);
        writer.Write(RightFrontArmScale);
        writer.Write(LeftFrontArmOpacity);
        writer.Write(RightFrontArmOpacity);
        writer.Write(LilyScale);
        writer.Write(HeadOpacity);

        // Write vector data.
        writer.WriteVector2(DesiredDistortionCenterOverride ?? Vector2.Zero);
        writer.WriteVector2(TargetPositionBeforeDimensionShift);
        writer.WriteVector2(TargetPositionAtStart);
        writer.WriteVector2(FrostScreenSmashLineCenter);
        writer.WriteVector2(LegScale);

        // Write shadow arm data.
        writer.Write(ShadowArms.Count);
        for (int i = 0; i < ShadowArms.Count; i++)
            ShadowArms[i].WriteTo(writer);

        // Write telegraph data.
        writer.Write(TwinkleSmashDetails.Count);
        for (int i = 0; i < TwinkleSmashDetails.Count; i++)
            TwinkleSmashDetails[i].Write(writer);

        // Write state data.
        var stateStack = (StateMachine?.StateStack ?? new Stack<EntityAIState<AvatarAIType>>()).ToList();
        writer.Write(stateStack.Count);
        for (int i = stateStack.Count - 1; i >= 0; i--)
        {
            writer.Write(stateStack[i].Time);
            writer.Write((byte)stateStack[i].Identifier);
        }
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        // Read binary data.
        BitsByte b1 = reader.ReadByte();
        FrontArmsAreDetached = b1[0];
        WaitingForPhase3Transition = b1[1];
        WaitingForDeathAnimation = b1[2];
        HasSentPlayersThroughVortex = b1[3];
        NeedsToSelectNewDimensionAttacksSoon = b1[4];

        // Read numerical data.
        FrostScreenSmashLineSlope = reader.ReadSingle();
        ParadiseReclaimed_LayerHeightBoost = reader.ReadInt32();
        ParadiseReclaimed_RenderStaticWallYPosition = reader.ReadSingle();
        ParadiseReclaimed_StaticPartInterpolant = reader.ReadSingle();
        PreviousState = (AvatarAIType)reader.ReadInt32();
        FirstDimensionAttack = (AvatarAIType)reader.ReadInt32();
        CurrentPhase = reader.ReadInt32();
        AttackPatternCounter = reader.ReadInt32();
        BorderAppearanceDelay = reader.ReadInt32();
        NPC.Opacity = reader.ReadSingle();
        NeckAppearInterpolant = reader.ReadSingle();
        LeftFrontArmScale = reader.ReadSingle();
        RightFrontArmScale = reader.ReadSingle();
        LeftFrontArmOpacity = reader.ReadSingle();
        RightFrontArmOpacity = reader.ReadSingle();
        LilyScale = reader.ReadSingle();
        HeadOpacity = reader.ReadSingle();

        // Read vector data.
        DesiredDistortionCenterOverride = reader.ReadVector2();
        if (DesiredDistortionCenterOverride == Vector2.Zero)
            DesiredDistortionCenterOverride = null;
        TargetPositionBeforeDimensionShift = reader.ReadVector2();
        TargetPositionAtStart = reader.ReadVector2();
        FrostScreenSmashLineCenter = reader.ReadVector2();
        LegScale = reader.ReadVector2();

        ShadowArms.Clear();
        int shadowArmCount = reader.ReadInt32();
        for (int i = 0; i < shadowArmCount; i++)
            ShadowArms.Add(AvatarShadowArm.ReadFrom(reader));

        // Read telegraph data.
        int telegraphCount = reader.ReadInt32();
        TwinkleSmashDetails = new(telegraphCount);
        for (int i = 0; i < TwinkleSmashDetails.Count; i++)
            TwinkleSmashDetails.Add(TwinkleSmashTelegraphSet.Read(reader));

        // Read state data.
        StateMachine.StateStack.Clear();
        int stateStackCount = reader.ReadInt32();
        for (int i = 0; i < stateStackCount; i++)
        {
            int time = reader.ReadInt32();
            byte stateType = reader.ReadByte();
            StateMachine.StateStack.Push(StateMachine.StateRegistry[(AvatarAIType)stateType]);
            StateMachine.StateRegistry[(AvatarAIType)stateType].Time = time;
        }
    }

    #endregion Networking

    #region AI
    public override void AI()
    {
        // Pick a target if the current one is invalid.
        bool invalidTargetIndex = Target.Invalid;
        if (invalidTargetIndex)
            TargetClosest();

        if (!NPC.WithinRange(Target.Center, 4972f))
            TargetClosest();

        if (TargetPositionAtStart == Vector2.Zero)
        {
            TargetPositionAtStart = Target.Center;
            NPC.netUpdate = true;
        }

        // Perform a state safety check before anything else.
        PerformStateSafetyCheck();

        // Grant all players infinite flight and ensure that they receive the boss effects buff.
        if (NPC.HasPlayerTarget)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead)
                    continue;

                player.wingTime = player.wingTimeMax;
                CalamityCompatibility.GrantInfiniteCalFlight(player);
                CalamityCompatibility.GrantBossEffectsBuff(player);
            }
        }

        // Disable rain and sandstorms.
        Main.StopRain();
        if (Main.netMode != NetmodeID.MultiplayerClient && Sandstorm.Happening)
            Sandstorm.StopSandstorm();

        // Set the global NPC instance.
        Myself = NPC;

        // Reset world events, since they cause MASSIVE visibility issues.
        Main.bloodMoon = false;
        Main.eclipse = false;
        Main.slimeRain = false;

        // Summon Solyn if necessary.
        bool solynCanBeSummoned = !EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame && CurrentState != AvatarAIType.ParadiseReclaimed_FakeoutPhase && !Main.zenithWorld;
        if (solynCanBeSummoned)
            BattleSolyn.SummonSolynForBattle(NPC.GetSource_FromAI(), Target.Center, BattleSolyn.SolynAIType.FightAvatar);

        // Reset things every frame.
        if (CurrentState != AvatarAIType.Teleport)
            BodyBrightness = 1f;
        NPC.rotation = NPC.rotation.AngleTowards(0f, 0.01f);
        MaskRotation = MaskRotation.AngleTowards(0f, 0.008f);
        DrawnAsSilhouette = false;
        ParadiseReclaimedIsOngoing = false;
        BattleIsDone = false;
        IndexFingerAngle = null;
        RingFingerAngle = null;
        ThumbAngle = null;
        RiftVanishInterpolant = 0f;
        AmbientSoundVolumeFactor = 1f;
        SuckOpacity = 0f;
        SolynAction = NPC.dontTakeDamage || !NPC.chaseable || ZPosition >= 2.5f ? s => StandardSolynBehavior_FlyNearPlayer(s, NPC) : StandardSolynBehavior_AttackAvatar;
        HideBar = false;
        NPC.damage = 0;
        NPC.defense = NPC.defDefense;
        NPC.takenDamageMultiplier = 1f;
        NPC.immortal = false;
        NPC.dontTakeDamage = CurrentState == AvatarAIType.EndCreditsScene;
        NPC.ShowNameOnHover = true;
        TileDisablingSystem.TilesAreUninteractable = false;
        ModReferences.Calamity?.Call("SetDRSpecific", NPC, DefaultDR);

        // Handle Nameless fight interactions.
        HandleNamelessDeityFightInteractions();

        if (AttackDimensionRelationship.TryGetValue(CurrentState, out AvatarDimensionVariant? dimension))
            AvatarOfEmptinessSky.Dimension = dimension;
        else if (CurrentState != AvatarAIType.Teleport && CurrentState != AvatarAIType.TeleportAbovePlayer && CurrentState != AvatarAIType.SendPlayerToMyUniverse && CurrentState != AvatarAIType.LeaveAfterPlayersAreDead && AITimer >= 5)
            AvatarOfEmptinessSky.Dimension = null;

        // Get rid of all falling stars. Their noises completely ruin the ambience.
        // active = false must be used over Kill because the Kill method causes them to drop their fallen star items.
        var fallingStars = AllProjectilesByID(ProjectileID.FallingStar);
        foreach (Projectile star in fallingStars)
            star.active = false;

        // Make afterimage opacities dissipate.
        LeftFrontArmAfterimageOpacity = Saturate(LeftFrontArmAfterimageOpacity - 0.11f);
        RightFrontArmAfterimageOpacity = Saturate(RightFrontArmAfterimageOpacity - 0.11f);

        // Teleport above the player on the first frame if necessary.
        // Don't make this too far from the player if you modify this, because if it's too far then it's possible they'll be inside the distortion zone and take an immediate unfair hit due to being
        // far away.
        if (ShouldTeleportAbovePlayer)
        {
            NPC.Center = Target.Center - Vector2.UnitY * ZPositionScale * 800f;
            ShouldTeleportAbovePlayer = false;
        }

        // Make the grip slowly dissipate.
        HandGraspAngle = HandGraspAngle.AngleTowards(0f, 0.009f);
        HandBaseGraspAngle = HandBaseGraspAngle.AngleTowards(0f, 0.009f);

        // Hold HP in place while waiting to enter the next phase.
        if (!Phase3 && LifeRatio <= Phase3LifeRatio && !WaitingForDeathAnimation)
            WaitingForPhase3Transition = true;
        if (WaitingForPhase3Transition)
        {
            if (Phase3 && AvatarOfEmptinessSky.Dimension is not null)
                WaitingForPhase3Transition = false;

            NPC.life = (int)Round(NPC.lifeMax * Phase3LifeRatio);
            NPC.immortal = true;
        }
        if (WaitingForDeathAnimation)
        {
            NPC.life = 1;
            NPC.immortal = true;
        }
        if (!Phase4 && LifeRatio <= Phase4LifeRatio)
        {
            CurrentPhase = 2;
            NeedsToSelectNewDimensionAttacksSoon = true;
            NPC.netUpdate = true;
        }

        // DELETE THE FUCKING PILLARS I HATE THESE SO FUCKING MUCH
        // DIE
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.type == NPCID.LunarTowerNebula || npc.type == NPCID.LunarTowerSolar || npc.type == NPCID.LunarTowerStardust || npc.type == NPCID.LunarTowerVortex)
                npc.active = false;
        }
        NPC.MoonLordCountdown = 0;

        // Handle AI behaviors.
        StateMachine.PerformBehaviors();
        if (AvatarOfEmptinessSky.Dimension is not null)
            TileDisablingSystem.TilesAreUninteractable = true;
        if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.CryonicDimension)
        {
            float windSpeed = Lerp(-12.87f, -23.11f, AperiodicSin(FightTimer / 1900f) * 0.5f + 0.5f) * AvatarOfEmptinessSky.WindSpeedFactor;
            Main.windSpeedCurrent = windSpeed;
            Main.windSpeedTarget = windSpeed;
        }
        else if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.VisceralDimension)
        {
            float windSpeed = Lerp(-2.94f, -3.11f, AperiodicSin(FightTimer / 1400f) * 0.5f + 0.5f) * AvatarOfEmptinessSky.WindSpeedFactor;
            Main.windSpeedCurrent = windSpeed;
            Main.windSpeedTarget = windSpeed;
        }
        else if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.FogDimension || AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.DarkDimension)
        {
            Main.windSpeedCurrent *= 0.9f;
            Main.windSpeedTarget = 0f;
        }
        else
        {
            Main.windSpeedTarget = -1.35f;
            Main.windSpeedCurrent = Lerp(Main.windSpeedCurrent, Main.windSpeedTarget, 0.05f);
        }

        if (Phase3 && !ParadiseReclaimedIsOngoing && CurrentState != AvatarAIType.TravelThroughVortex && CurrentState != AvatarAIType.ParadiseReclaimed_FakeoutPhase && CurrentState != AvatarAIType.SendPlayerToMyUniverse)
            Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/AvatarOfEmptinessP3");
        if (CurrentState == AvatarAIType.SendPlayerToMyUniverse && AvatarOfEmptinessSky.Dimension is null)
            Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/AvatarOfEmptinessP2");

        // Update ambient looping sounds.
        UpdateLoopingSounds();

        // Update the state machine.
        if (Main.netMode != NetmodeID.MultiplayerClient)
            StateMachine.PerformStateTransitionCheck();

        // Make the teleport delay countdown decrement.
        TeleportDelayCountdown = Utils.Clamp(TeleportDelayCountdown - 1, 0, 600);

        // Decide on compass and depth meter text before the player enters the Avatar's universe.
        if (Main.netMode != NetmodeID.Server && !TileDisablingSystem.TilesAreUninteractable && Main.rand.NextBool(120))
        {
            HorizontalParsecs = Utils.RandomNextSeed((ulong)Main.rand.Next(int.MaxValue)) / 100;
            VerticalParsecs = Utils.RandomNextSeed(HorizontalParsecs) / 200;
        }

        // Reattach the Avatar's arms.
        if (CurrentState != AvatarAIType.Unclog && CurrentState != AvatarAIType.Teleport)
            FrontArmsAreDetached = false;

        // Do not despawn.
        NPC.timeLeft = 7200;

        // Disable hover text when invisible.
        if (NPC.Opacity < 0.5f)
            NPC.ShowNameOnHover = false;

        // Disable damage when invisible.
        if (NPC.Opacity <= 0.35f)
        {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
        }

        // Update liquids.
        UpdateLiquids();

        // Update afterimages.
        UpdateArmAfterimagePositions();

        // Update blood droplets.
        UpdateBloodDropletOverlay();

        // Make the spider lily glow boost dissipate.
        if (CurrentState != AvatarAIType.Teleport)
            LilyGlowIntensityBoost = Clamp(LilyGlowIntensityBoost * 0.98f - 0.012f, 0f, 4000f);

        // Make the distortion intensity approach its ideal value.
        if (BorderAppearanceDelay > 0)
            BorderAppearanceDelay--;

        UpdateDistanceDistortionShader();

        // Make the Avatar's apparent position not move as much if he's in the background.
        if (CurrentState != AvatarAIType.AntishadowOnslaught)
            ApplyParallaxPositioning();

        // Hide HP bars if necessary.
        if (HideBar)
        {
            // Tried to use an unsafe accessor for this but it failed. I might just be dumb?
            IBigProgressBar? currentBar = (IBigProgressBar?)currentBossBar?.GetValue(Main.BigBossProgressBar);
            if (currentBar is AvatarBossBar)
                currentBossBar?.SetValue(Main.BigBossProgressBar, null);

            CalamityCompatibility.MakeCalamityBossBarClose(NPC);
        }

        // Increment timers.
        PerformStateSafetyCheck();
        AITimer++;
        FightTimer++;

        RiftRotationSpeedInterpolant = Lerp(RiftRotationSpeedInterpolant, SuckOpacity, 0.037f);
        RiftRotationTimer += Lerp(0.09f, 0.27f, RiftRotationSpeedInterpolant) / 60f;

        // Disable map overlay bugs.
        if (AvatarOfEmptinessSky.Dimension is not null)
        {
            Main.mapStyle = 0;
            Main.mapFullscreen = false;
        }

        if (CalamityCompatibility.Enabled)
            CreateEnrageParticles();

        // Scuffed thing. Used to ensure that the antishadow background immediately (dis)appears as needed, rather than having a several frame buffer.
        for (int i = 0; i < 32; i++)
            ShaderManager.GetFilter("NoxusBoss.AntishadowSilhouetteShader")?.Update();

        // Disable the sulph sea background, since it has a tendency to overlay the boss background.
        SulphSeaSkyDisabler.DisableSulphSeaSky = true;
    }

    public void UpdateLoopingSounds()
    {
        if (WindAmbienceLoop is null || WindAmbienceLoop.HasBeenStopped)
        {
            WindAmbienceLoop = LoopedSoundManager.CreateNew(GennedAssets.Sounds.Avatar.Ambience, () =>
            {
                return !NPC.active;
            });
        }
        if (CurrentState == AvatarAIType.SendPlayerToMyUniverse)
        {
            DimensionAmbienceLoop?.Stop();
            DimensionAmbienceLoop = null;
        }
        else if ((DimensionAmbienceLoop is null || DimensionAmbienceLoop.HasBeenStopped) && AvatarOfEmptinessSky.Dimension is not null && AvatarOfEmptinessSky.Dimension.AmbientLoopSound is not null)
        {
            DimensionAmbienceLoop?.Stop();
            DimensionAmbienceLoop = LoopedSoundManager.CreateNew(AvatarOfEmptinessSky.Dimension.AmbientLoopSound.Value, () =>
            {
                return !NPC.active || AvatarOfEmptinessSky.Dimension is null;
            });
        }

        WindAmbienceLoop.Update(Main.LocalPlayer.Center, sound =>
        {
            float idealVolume = Pow(AvatarOfEmptinessSky.WindSpeedFactor, 1.62f) * MusicVolumeManipulationSystem.MuffleFactor * AmbientSoundVolumeFactor * 0.8f;
            if (AvatarOfEmptinessSky.Dimension is not null || CurrentState == AvatarAIType.ParadiseReclaimed_FakeoutPhase || ModContent.GetInstance<EndCreditsScene>().IsActive)
                idealVolume = 0.001f;

            if (sound.Volume != idealVolume)
                sound.Volume = idealVolume;
        });
        DimensionAmbienceLoop?.Update(Main.LocalPlayer.Center, sound =>
        {
            float idealVolume = AmbientSoundVolumeFactor * 0.65f;
            if (sound.Volume != idealVolume)
                sound.Volume = idealVolume;
        });

        // This sound does NOT terminate when the SuckOpacity is below a certain threshold, it merely has a volume of zero, and plays at all times.
        // The reason for this is so that the sound starts at a random time when it actually needs to be played.
        if (SuctionLoop is null || SuctionLoop.HasBeenStopped)
        {
            SuctionLoop = LoopedSoundManager.CreateNew(GennedAssets.Sounds.Avatar.SuctionLoop, () =>
            {
                return !NPC.active;
            });
        }
        SuctionLoop?.Update(Vector2.Lerp(NPC.Center, Main.LocalPlayer.Center, 0.75f), sound =>
        {
            sound.Volume = SuckOpacity * (1f - AvatarRiftSuckVisualsManager.ZoomInInterpolant) * 2.95f + 1e-5f;
        });
    }

    public void UpdateBloodDropletOverlay()
    {
        if (BloodDropletOverlayInterpolant <= 0f || Main.netMode == NetmodeID.Server)
            return;

        BloodDropletOverlayInterpolant = Saturate(BloodDropletOverlayInterpolant - 0.005f);

        ManagedScreenFilter overlayShader = ShaderManager.GetFilter("NoxusBoss.BloodDropletOverlayShader");
        overlayShader.TrySetParameter("dropletDissipateSpeed", 0.4f);
        overlayShader.TrySetParameter("animationCompletion", 1f - BloodDropletOverlayInterpolant);
        overlayShader.SetTexture(PerlinNoise.Value, 1, SamplerState.LinearWrap);
        overlayShader.Activate();
    }

    public void PerformStateSafetyCheck()
    {
        if (StateMachine is null || StateMachine.StateStack is null)
            return;

        // Add the relevant phase cycle if it has been exhausted, to ensure that the Avatar's attacks are cyclic.
        if (StateMachine.StateStack.Count <= 0 || StateMachine.StateStack.Count == 0)
            StateMachine.StateStack.Push(StateMachine.StateRegistry[AvatarAIType.ResetCycle]);
    }

    public void UpdateLiquids()
    {
        for (int i = 0; i < 8; i++)
            LiquidDrawContents?.UpdateLiquid(NPC.Center + Vector2.UnitY * (1f - SuckOpacity) * 145f);
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    public void CreateEnrageParticles()
    {
        if (!NPC.HasBuff<Enraged>() || HeadOpacity <= 0f || HeadScale < 0.5f || NPC.Opacity < 0.5f || ZPositionScale < 0.51f)
            return;

        if (Main.rand.NextBool(10))
        {
            CartoonAngerParticle anger = new CartoonAngerParticle(HeadPosition + (new Vector2(48f, -68f) + Main.rand.NextVector2CircularEdge(40f, 40f)) * ZPositionScale, Vector2.Zero, Color.Red, Main.rand.NextFloat(0.7f, 1.2f), 35);
            anger.Spawn();
        }
    }

    public void UpdateDistanceDistortionShader()
    {
        IdealDistortionIntensity *= InverseLerp(60f, 0f, BorderAppearanceDelay);
        DistortionIntensity = Lerp(DistortionIntensity, IdealDistortionIntensity, 0.056f);

        // Make the music fade out if the player is inside of the distortion.
        float distortionRadiusCutoff = 1250f;
        float distortionRadiusIntensityRange = 4000f;
        float distanceFromCenter = (DesiredDistortionCenterOverride ?? NPC.Center).Distance(Target.Center);
        float modifiedDistanceFromCenter = (distanceFromCenter - distortionRadiusCutoff) * IdealDistortionIntensity;
        if (IdealDistortionIntensity >= 0.01f)
        {
            float distortionSoundFade = Utils.Remap(modifiedDistanceFromCenter, 450f, distortionRadiusIntensityRange * 1.5f, 1f, 0.001f);
            SoundMufflingSystem.MuffleFactor = MathF.Min(SoundMufflingSystem.MuffleFactor, distortionSoundFade);

            float remap = Utils.Remap(modifiedDistanceFromCenter, 450f, distortionRadiusIntensityRange * 1.25f, 1f, 0.01f);

            // Hurt the target if they stray too far from the Avatar.
            if (remap < 0.3f)
            {
                if (NPC.HasPlayerTarget)
                    Main.player[NPC.target].Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), Main.rand.Next(900, 950), 0);
                else if (NPC.HasNPCTarget)
                    Main.npc[NPC.TranslatedTargetIndex].SimpleStrikeNPC(1500, 0);
            }
            if (CurrentState == AvatarAIType.TravelThroughVortex && NPC.HasPlayerTarget)
            {
                while (remap < 0.4f)
                {
                    distanceFromCenter = (DesiredDistortionCenterOverride ?? NPC.Center).Distance(Target.Center);
                    modifiedDistanceFromCenter = (distanceFromCenter - distortionRadiusCutoff) * IdealDistortionIntensity;
                    remap = Utils.Remap(modifiedDistanceFromCenter, 450f, distortionRadiusIntensityRange * 1.25f, 1f, 0.01f);
                    Main.player[NPC.TranslatedTargetIndex].Center += Main.player[NPC.TranslatedTargetIndex].SafeDirectionTo(NPC.Center) * 2f;
                }
            }
        }

        if (TileDisablingSystem.TilesAreUninteractable && CurrentState != AvatarAIType.RealityShatter_DimensionTwist && CurrentState != AvatarAIType.Unclog && CurrentState != AvatarAIType.TravelThroughVortex)
        {
            IdealDistortionIntensity = 0f;
            DistortionIntensity = 0f;
        }

        DistortionCenter = Vector2.Lerp(DistortionCenter, DesiredDistortionCenterOverride ?? NPC.Center, 0.134f);
        if (FightTimer <= 5)
            DistortionCenter = NPC.Center;

        if (Main.netMode == NetmodeID.Server)
            return;

        ManagedScreenFilter distortionShader = ShaderManager.GetFilter("NoxusBoss.AvatarDistanceDistortionShader");
        distortionShader.TrySetParameter("screenResolution", Main.ScreenSize.ToVector2() / Main.GameViewMatrix.Zoom);
        distortionShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
        distortionShader.TrySetParameter("distortionStartRadius", distortionRadiusCutoff / (IdealDistortionIntensity + 0.00001f));
        distortionShader.TrySetParameter("distortionRadiusIntensityRange", distortionRadiusIntensityRange);
        distortionShader.TrySetParameter("avatarPosition", Vector2.Transform(DistortionCenter - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
        distortionShader.SetTexture(WatercolorNoiseA, 1, SamplerState.LinearWrap);
        distortionShader.Activate();
    }

    public void HandleNamelessDeityFightInteractions()
    {
        // Take damage from NPCs and projectiles if fighting Nameless deity.
        NPCID.Sets.TakesDamageFromHostilesWithoutBeingFriendly[Type] = NPC.HasNPCTarget;

        // Stay in the world if fighting Nameless.
        if (NPC.HasNPCTarget && NPC.position.Y <= 1600f)
            NPC.position.Y = Main.maxTilesY * 16f - 250f;
        if (NPC.HasNPCTarget && NPC.position.Y >= Main.maxTilesY * 16f - 900f)
            NPC.position.Y = 2300f;

        if (NPC.HasNPCTarget && NPC.position.X <= 400f)
            NPC.position.X = Main.maxTilesX * 16f - 900f;
        if (NPC.HasNPCTarget && NPC.position.X >= Main.maxTilesX * 16f + 400f)
            NPC.position.X = 900f;

        // Take far, far more damage if fighting Nameless.
        NPC.takenDamageMultiplier *= NPC.HasNPCTarget ? 1000f : 1f;

        // Take 50x more damage if Nameless is present and using a glock.
        if (NamelessDeityBoss.Myself_CurrentState == NamelessDeityBoss.NamelessAIType.Glock)
            NPC.takenDamageMultiplier *= 50f;
    }

    public void UpdateArmAfterimagePositions()
    {
        // Update the trail position cache.
        for (int i = PastLeftArmPositions.Length - 1; i >= 1; i--)
            PastLeftArmPositions[i] = PastLeftArmPositions[i - 1];
        for (int i = PastRightArmPositions.Length - 1; i >= 1; i--)
            PastRightArmPositions[i] = PastRightArmPositions[i - 1];

        PastLeftArmPositions[0] = LeftArmPosition;
        PastRightArmPositions[0] = RightArmPosition;
    }

    public void ApplyParallaxPositioning()
    {
        if (Main.netMode != NetmodeID.SinglePlayer)
            return;

        // God.
        // This ensures that the Avatar's apparent position isn't as responsive to camera movements if he's in the background, giving a pseudo-parallax visual.
        // Idea is basically the Avatar going
        // "Oh? You moved 30 pixels in this direction? Well I'm in the background bozo so I'm gonna follow you and go in the same direction by, say, 27 pixels. This will make it look like I only moved 3 pixels"
        float parallax = 1f - Pow(2f, ZPosition * -0.8f);
        if (NPC.HasPlayerTarget)
        {
            Player playerTarget = Main.player[NPC.TranslatedTargetIndex];
            Vector2 targetOffset = playerTarget.position - playerTarget.oldPosition;
            NPC.position += targetOffset * Saturate(parallax);
        }
    }

    #endregion AI

    #region Collision and Hit Effects

    public bool CanBeHitBy(Rectangle hitbox) => hitbox.Intersects(SpiderLilyHitbox);

    public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) => OnHitPlayerEvent?.Invoke(target, hurtInfo);

    public override bool? CanBeHitByProjectile(Projectile projectile)
    {
        if (!projectile.Colliding(projectile.Hitbox, SpiderLilyHitbox))
            return false;
        if (projectile.ModProjectile is not null and IProjOwnedByBoss<NamelessDeityBoss>)
            return true;

        return null;
    }

    public override bool CanBeHitByNPC(NPC attacker) => CanBeHitBy(attacker.Hitbox);

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        if (CurrentState == AvatarAIType.FrostScreenSmash_SmashForeground)
        {
            Vector2 perpendicularHurtZoneDirection = FrostScreenSmashLineDirection.RotatedBy(PiOver2);
            float directionDot = Vector2.Dot(target.Center - FrostScreenSmashLineCenter, perpendicularHurtZoneDirection);
            return Sign(directionDot) == Sign(FrostScreenSmashLineDirection.X);
        }

        return NPC.WithinRange(target.Center, NPC.scale * Saturate(ZPositionScale) * 312f);
    }

    // Timed DR but a bit different. I'm typically very, very reluctant towards this mechanic, but given that this boss exists in shadowspec tier, I am willing to make
    // an exception. This will not cause the dumb "lol do 0 damage for 30 seconds" problems that Calamity had in the past.
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        // Calculate how far ahead Nameless' HP is relative to how long he's existed so far.
        // This would be one if you somehow got him to death on the first frame of the fight.
        // This naturally tapers off as the fight goes on.
        float fightLengthInterpolant = InverseLerp(0f, MinutesToFrames(IdealFightDurationInMinutes), FightTimer);
        float aheadOfFightLengthInterpolant = MathF.Max(0f, 1f - fightLengthInterpolant - LifeRatio);

        float damageReductionInterpolant = Pow(aheadOfFightLengthInterpolant, 0.45f);
        float damageReductionFactor = Lerp(1f, 1f - MaxTimedDRDamageReduction, damageReductionInterpolant);
        modifiers.FinalDamage *= damageReductionFactor;
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (NPC.soundDelay >= 1)
            return;

        NPC.soundDelay = 9;
        SoundEngine.PlaySound(GennedAssets.Sounds.NPCHit.AvatarHurt with { MaxInstances = 0, Volume = ZPositionScale, PitchVariance = 0.4f }, NPC.Center);
    }

    public override bool CheckDead()
    {
        // Disallow natural death. It is only permitted if the Avatar is performing the last state behavior of the death animation.
        if (CurrentState == AvatarAIType.ParadiseReclaimed_FakeoutPhase)
            return true;

        // Keep the Avatar's HP at its minimum.
        NPC.life = 1;

        // Wait for the death animation.
        if (!WaitingForDeathAnimation)
        {
            CurrentPhase = 2;
            WaitingForDeathAnimation = true;
            NPC.dontTakeDamage = true;
            NPC.netUpdate = true;
        }
        return false;
    }

    #endregion Collision and Hit Effects
}
