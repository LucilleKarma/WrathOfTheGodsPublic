using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Content.Particles.Metaballs;
using NoxusBoss.Core.Autoloaders;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Graphics.ScreenShake;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Graphics.SwagRain;
using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.World.GameScenes.AvatarAppearances;
using NoxusBoss.Core.World.GameScenes.OldDukeDeath;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using NoxusBoss.Core.World.Subworlds;
using NoxusBoss.Core.World.WorldSaving;

using ReLogic.Utilities;

using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

using static NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.AvatarOfEmptiness;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;

public class AvatarRift : ModNPC, IBossDowned
{
    #region Custom Types and Enumerations

    public enum RiftAttackType
    {
        Awaken,
        RedirectingDisgustingStars,
        ReleaseOtherworldlyThorns,
        SuckPlayerIn,
        PlasmaBlasts,
        FlailingFrenzy,
        TrembleInPlace,

        // Animation-specific AI states. These are not necessarily related to the actual battle.
        KillOldDuke,

        DeathAnimation
    }

    #endregion Custom Types and Enumerations

    #region Fields and Properties

    public bool AutomaticallyRegisterDeathGlobally => true;

    private static NPC? myself;

    /// <summary>
    /// An optional override for render target calculations.
    /// </summary>
    public int? TargetIdentifierOverride
    {
        get;
        set;
    }

    /// <summary>
    /// The despawn timer. Once this fully elapses, the rift will immediately disappear.
    /// </summary>
    public int DespawnTimer
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
    /// The scale the Avatar had on the previous frame. Used to determine if the current scale is increasing or decreasing.
    /// </summary>
    public float PreviousScale
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the rift's red eye dots.
    /// </summary>
    public float EyesOpacity
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
    /// The draw offset of the rift's red eye dots.
    /// </summary>
    public Vector2 EyesDrawOffset
    {
        get;
        set;
    }

    /// <summary>
    /// The desired draw offset of the rift's red eye dots.
    /// </summary>
    public Vector2 EyesDrawOffsetDestination
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
    /// The collection of sounds that are attached to the Avatar's position at all times.
    /// </summary>
    public List<SlotId> AttachedSounds
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// The collection of all of the Avatar's arms.
    /// </summary>
    public List<AvatarShadowArm> Arms
    {
        get;
        set;
    } = [];

    /// <summary>
    /// The ambient portal sound loop.
    /// </summary>
    public LoopedSoundInstance? AmbienceLoop
    {
        get;
        private set;
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
    /// The upcoming attack set queue.
    /// </summary>
    public Queue<RiftAttackType> AttackQueue
    {
        get;
        set;
    } = [];

    /// <summary>
    /// A 0-1 interpolant that represents how strong suck visuals are during the suck attack.
    /// </summary>
    public float SuckOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The pitch of the <see cref="SuctionLoop"/> instance.
    /// </summary>
    public float SuckSoundPitch
    {
        get;
        set;
    }

    /// <summary>
    /// The overriding volume of the <see cref="SuctionLoop"/> instance.
    /// </summary>
    public float? SuckSoundVolumeOverride
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this rift should be drawn with no matrix scaling.
    /// </summary>
    public bool DrawnFromTelescope
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this rift is a background prop.
    /// </summary>
    public bool BackgroundProp
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of HP the Avatar has, as a 0-1 ratio.
    /// </summary>
    public float LifeRatio => NPC.life / (float)NPC.lifeMax;

    /// <summary>
    /// the Avatar's current target.
    /// </summary>
    public Player Target => Main.player[NPC.target];

    /// <summary>
    /// The current AI state.
    /// </summary>
    public RiftAttackType CurrentAttack
    {
        get => (RiftAttackType)NPC.ai[0];
        set => NPC.ai[0] = (int)value;
    }

    /// <summary>
    /// The transform this rift should use when drawing.
    /// </summary>
    public Matrix TransformPerspective
    {
        get
        {
            if (DrawnFromTelescope)
                return Matrix.Identity;

            if (BackgroundProp)
                return Main.GameViewMatrix.EffectMatrix;

            if (NPC.IsABestiaryIconDummy)
                return Main.UIScaleMatrix;

            return Main.GameViewMatrix.TransformationMatrix;
        }
    }

    /// <summary>
    /// A general-purpose AI timer. Is reset when <see cref="CurrentAttack"/> is naturally switched.
    /// </summary>
    public ref float AITimer => ref NPC.ai[1];

    /// <summary>
    /// the Avatar's <see cref="NPC"/> instance. Returns <see langword="null"/> if the Avatar is not present.
    /// </summary>
    public static NPC? Myself
    {
        get
        {
            if (Main.gameMenu)
                return myself = null;

            if (myself is null)
                return null;

            if (!myself.active)
                return null;

            if (myself.type != ModContent.NPCType<AvatarRift>())
                return null;

            return myself;
        }
        private set => myself = value;
    }

    /// <summary>
    /// The render target that holds all arm render data.
    /// </summary>
    public static AvatarFirstPhaseArmTargetContent ArmDrawContents
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target that holds all rift render data.
    /// </summary>
    public static InstancedRequestableTarget RiftDrawContents
    {
        get;
        private set;
    }

    /// <summary>
    /// The visual handler for the Avatar's liquid visuals.
    /// </summary>
    public AvatarRiftLiquidInfo LiquidDrawContents
    {
        get;
        private set;
    }

    /// <summary>
    /// The amount of damage disgusting stars do.
    /// </summary>
    public static int DisgustingStarDamage => GetAIInt("DisgustingStarDamage");

    /// <summary>
    /// The amount of damage rubble created during the suck attack does.
    /// </summary>
    public static int RubbleDamage => GetAIInt("RubbleDamage");

    /// <summary>
    /// The default hitbox size of the Avatar's rift in phase 1.
    /// </summary>
    public static readonly Vector2 DefaultHitboxSize = new Vector2(528f, 612f);

    /// <summary>
    /// The amount of damage reduction the Avatar's first phase has overall.
    /// </summary>
    public static float DefaultDR => GetAIFloat("DefaultDR_Rift");

    /// <summary>
    /// The HP ratio threshold upon which the first phase ends and the second phase is started.
    /// </summary>
    public static float TerminationLifeRatio => GetAIFloat("TerminationLifeRatio");

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Avatar/FirstPhaseForm", Name);

    #endregion Fields and Properties

    #region Initialization

    public override void Load()
    {
        string contentPath = GetAssetPath("Content/Items/Placeable/MusicBoxes", "AvatarRiftMusicBox");
        string musicPath = "Assets/Sounds/Music/AvatarRift";
        MusicBoxAutoloader.Create(Mod, contentPath, musicPath, out _, out _, DrawMusicBoxWithBackRift);
    }

    internal static bool DrawMusicBoxWithBackRift(int tileID, int x, int y)
    {
        Tile t = Framing.GetTileSafely(x, y);
        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 drawPosition = new Vector2(x * 16 - Main.screenPosition.X, y * 16 - Main.screenPosition.Y + 2f) + drawOffset;

        // Calculate the top left of the tile.
        int frameX = t.TileFrameX;
        int frameY = t.TileFrameY;
        if (frameX % 36 == 0 && frameY == 0)
        {
            Texture2D rift = GennedAssets.Textures.MusicBoxes.MusicBoxRift.Value;
            float riftRotation = 0f;
            if (frameX >= 36)
                riftRotation = Main.GlobalTimeWrappedHourly * 0.67f + x * 1.74f + y * 0.72f;

            Main.spriteBatch.Draw(rift, drawPosition + new Vector2(14f, 12f), null, Color.White, riftRotation, Vector2.One * 19f, 1f, 0, 0f);
        }

        // Draw the music box.
        Texture2D mainTexture = TextureAssets.Tile[tileID].Value;
        Color lightColor = Lighting.GetColor(x, y).MultiplyRGB(new Color(198, 198, 198));
        Main.spriteBatch.Draw(mainTexture, drawPosition, new Rectangle(frameX, frameY, 16, 16), lightColor, 0f, Vector2.Zero, 1f, 0, 0f);

        return false;
    }

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.TrailingMode[NPC.type] = 3;
        NPCID.Sets.TrailCacheLength[NPC.type] = 90;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        EmptinessSprayer.NPCsToNotDelete[Type] = true;

        // Apply miracleblight immunities.
        CalamityCompatibility.MakeImmuneToMiracleblight(NPC);

        On_NPC.DoDeathEvents_DropBossPotionsAndHearts += DisableRiftBossDeathEffects;

        // Allow the Avatar's arms to optionally do contact damage.
        On_NPC.GetMeleeCollisionData += ExpandEffectiveHitboxForHands;

        // Register targets.
        if (Main.netMode != NetmodeID.Server)
        {
            ArmDrawContents = new AvatarFirstPhaseArmTargetContent();
            RiftDrawContents = new InstancedRequestableTarget();
            Main.ContentThatNeedsRenderTargets.Add(ArmDrawContents);
            Main.ContentThatNeedsRenderTargets.Add(RiftDrawContents);

            GraphicalUniverseImagerOptionManager.RegisterNew(new GraphicalUniverseImagerOption("Mods.NoxusBoss.UI.GraphicalUniverseImager.AvatarP1Background", true,
                GennedAssets.Textures.GraphicalUniverseImager.ShaderSource_Rift, RenderGUIPortrait, (minDepth, maxDepth, settings) =>
                {
                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin();
                    AvatarRiftSky.SkyTarget.Render(Color.White * ModContent.GetInstance<GraphicalUniverseImagerSky>().EffectiveIntensity, 1);

                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, GetCustomSkyBackgroundMatrix());
                }));
        }

        this.ExcludeFromBestiary();
    }

    private void DisableRiftBossDeathEffects(On_NPC.orig_DoDeathEvents_DropBossPotionsAndHearts orig, NPC self, ref string typeName)
    {
        if (self.type != Type)
            orig(self, ref typeName);
    }

    private void ExpandEffectiveHitboxForHands(On_NPC.orig_GetMeleeCollisionData orig, Rectangle victimHitbox, int enemyIndex, ref int specialHitSetter, ref float damageMultiplier, ref Rectangle npcRect)
    {
        orig(victimHitbox, enemyIndex, ref specialHitSetter, ref damageMultiplier, ref npcRect);

        // See the big comment in CanHitPlayer.
        if (Main.npc[enemyIndex].type == Type && Main.npc[enemyIndex].As<AvatarRift>().CurrentAttack == RiftAttackType.FlailingFrenzy)
            npcRect.Inflate(4000, 4000);
    }

    private static void RenderGUIPortrait(GraphicalUniverseImagerSettings settings)
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        AvatarRiftSky.SkyTarget.Render(Color.White);
        Main.spriteBatch.End();
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 50f;
        NPC.damage = 420;
        NPC.width = (int)DefaultHitboxSize.X;
        NPC.height = (int)DefaultHitboxSize.Y;
        NPC.defense = 100;
        NPC.lifeMax = GetAIInt("DefaultHP_Rift");
        if (CalamityCompatibility.Enabled)
            CalamityCompatibility.SetLifeMaxByMode_ApplyCalBossHPBoost(NPC);

        if (Main.expertMode)
        {
            NPC.damage = 624;

            // Fuck arbitrary Expert boosts.
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
        NPC.value = 0;
        NPC.netAlways = true;
        NPC.hide = true;
        NPC.BossBar = ModContent.GetInstance<AvatarBossBar>();
        CalamityCompatibility.MakeCalamityBossBarClose(NPC);
        LiquidDrawContents = new(40, 0.56f, completionRatio => ((1f - completionRatio) * 220f + 80f) * NPC.scale);

        Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/AvatarRift");
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange([
            new FlavorTextBestiaryInfoElement($"Mods.{Mod.Name}.Bestiary.{Name}"),
            new MoonLordPortraitBackgroundProviderBestiaryInfoElement()
        ]);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Arms.Count);
        for (int i = 0; i < Arms.Count; i++)
            Arms[i].WriteTo(writer);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        // Server syncs always AITimer + 1 instead of correct AITimer, causing clients to "skip" the first frame, causing some audio and visual issues.
        AITimer--;

        Arms.Clear();
        int armCount = reader.ReadInt32();
        for (int i = 0; i < armCount; i++)
            Arms.Add(AvatarShadowArm.ReadFrom(reader));
    }
    #endregion Initialization

    #region AI
    public override void AI()
    {
        // Pick a target if the current one is invalid.
        bool invalidTargetIndex = NPC.target is < 0 or >= 255;
        if (invalidTargetIndex)
            NPC.TargetClosest();

        bool invalidTarget = Target.dead || !Target.active;
        if (invalidTarget)
            NPC.TargetClosest();

        if (!NPC.WithinRange(Target.Center, 4600f - Target.aggro))
            NPC.TargetClosest();

        // Hey bozo the player's gone. Leave.
        if ((Target.dead || !Target.active) && Main.netMode != NetmodeID.MultiplayerClient)
        {
            DespawnTimer++;
            if (DespawnTimer >= 6)
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

        // Force rain.
        Main.raining = true;
        Main.windSpeedTarget = -0.63f;
        Main.maxRaining = 0f;
        Main.cloudAlpha = 0.4f;
        Main.rainTime = SecondsToFrames(4f);

        // Set the global NPC instance.
        Myself = NPC;

        // Reset things every frame.
        SolynAction = StandardSolynBehavior_AttackAvatar;
        if (NPC.dontTakeDamage || !NPC.chaseable)
            SolynAction = s => StandardSolynBehavior_FlyNearPlayer(s, NPC);

        SuckSoundVolumeOverride = null;
        SuckSoundPitch = 0f;
        EyesOpacity *= 0.82f;
        HideBar = false;
        NPC.damage = NPC.defDamage;
        NPC.defense = NPC.defDefense;
        NPC.dontTakeDamage = false;
        NPC.ShowNameOnHover = true;
        ModReferences.Calamity?.Call("SetDRSpecific", NPC, DefaultDR);

        // Do not despawn.
        NPC.timeLeft = 7200;

        // Begin the death animation if ready.
        if (LifeRatio < TerminationLifeRatio && NPC.ai[2] == 0f)
            TriggerDeathAnimation();

        PreviousScale = NPC.scale;

        bool solynCanBeSummoned = !EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame && !Main.zenithWorld && CurrentAttack != RiftAttackType.KillOldDuke;
        if (solynCanBeSummoned)
            BattleSolyn.SummonSolynForBattle(NPC.GetSource_FromAI(), Target.Center, BattleSolyn.SolynAIType.FightAvatar);

        Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/AvatarRift");

        switch (CurrentAttack)
        {
            case RiftAttackType.Awaken:
                DoBehavior_Awaken();
                break;
            case RiftAttackType.RedirectingDisgustingStars:
                DoBehavior_RedirectingDisgustingStars();
                break;
            case RiftAttackType.ReleaseOtherworldlyThorns:
                DoBehavior_ReleaseOtherworldlyThorns();
                break;
            case RiftAttackType.SuckPlayerIn:
                DoBehavior_SuckPlayerIn();
                break;
            case RiftAttackType.PlasmaBlasts:
                DoBehavior_PlasmaBlasts();
                break;
            case RiftAttackType.FlailingFrenzy:
                DoBehavior_FlailingFrenzy();
                break;
            case RiftAttackType.TrembleInPlace:
                DoBehavior_TrembleInPlace();
                break;
            case RiftAttackType.KillOldDuke:
                DoBehavior_KillOldDuke();
                break;
            case RiftAttackType.DeathAnimation:
                DoBehavior_DeathAnimation();
                break;
        }

        // Shake the screen if disappearing or reappearing.
        if (NPC.scale <= 0.8f && NPC.scale >= 0.01f)
            CustomScreenShakeSystem.Start(6, 2.2f);

        // Update the ambient sounds.
        UpdateAmbientSounds();

        // Update attached sounds.
        UpdateAttachedSounds();

        // Disable damage when invisible.
        if (NPC.Opacity <= 0.35f)
        {
            NPC.ShowNameOnHover = false;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
        }

        if (HideBar)
            CalamityCompatibility.MakeCalamityBossBarClose(NPC);

        AITimer++;

        RiftRotationSpeedInterpolant = Lerp(RiftRotationSpeedInterpolant, SuckOpacity, 0.015f);
        RiftRotationTimer += Lerp(0.09f, 0.27f, RiftRotationSpeedInterpolant) / 60f;

        EyesDrawOffset = EyesDrawOffset.MoveTowards(EyesDrawOffsetDestination, 12.5f);

        // Update liquids.
        for (int i = 0; i < 4; i++)
            LiquidDrawContents?.UpdateLiquid(NPC.Center + Vector2.UnitY * (1f - SuckOpacity) * 145f);

        // Rotate based on horizontal speed.
        NPC.rotation = NPC.velocity.X * 0.004f;

        // Disable the sulph sea background, since it has a tendency to overlay the boss background.
        SulphSeaSkyDisabler.DisableSulphSeaSky = true;
    }

    public void DoBehavior_Awaken()
    {
        int screenRumbleTime = 320;
        int roarTime = 90;

        SolynAction = s => StandardSolynBehavior_FlyNearPlayer(s, NPC);

        // Disable damage.
        NPC.damage = 0;

        // Close the HP bar.
        HideBar = true;

        // Play an ominous sound at first.
        if (AITimer == 1f)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase1IntroWind);

        if (AITimer == screenRumbleTime - 105f)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase1IntroBassRumble);

        // Start out invisible.
        if (AITimer < screenRumbleTime)
        {
            NPC.Opacity = 0f;
            NPC.scale = 0f;
            NPC.Center = Target.Center - Vector2.UnitY * 430f;
        }

        // Create an explosion sound and appear.
        if (AITimer == screenRumbleTime)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(Language.GetTextValue("Announcement.HasAwoken", NPC.TypeName), 175, 75);
            else if (Main.netMode == NetmodeID.Server)
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasAwoken", NPC.GetTypeNetName()), new Color(175, 75, 255));

            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase1IntroAppear);
            TeleportTo(NPC.Center + Vector2.UnitY * 50f);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<DarkWave>(), 0, 0f, -1, 0f, 0f, 1f);

            CustomScreenShakeSystem.Start(roarTime, 14.5f);
        }

        // Roar after appearing.
        if (AITimer >= screenRumbleTime)
        {
            HideBar = false;
            NPC.Opacity = 1f;
            NPC.scale = Saturate(NPC.scale + 0.1f);

            if (AITimer % 5f == 1f && AITimer <= screenRumbleTime + roarTime - 45f)
            {
                Color burstColor = Color.Red;

                // Create blur and burst particle effects.
                ExpandingChromaticBurstParticle burst = new ExpandingChromaticBurstParticle(NPC.Center, Vector2.Zero, burstColor, 9, 0.1f, 2f);
                burst.Spawn();
                GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 0.7f, 24);
            }
        }
        else
            Music = 0;

        foreach (Player player in Main.ActivePlayers)
            CalamityCompatibility.ResetRippers(player);

        // Accelerate upward right before teleporting away.
        if (AITimer >= screenRumbleTime + roarTime - 24f)
            NPC.velocity = Vector2.Lerp(NPC.velocity, -Vector2.UnitY * 9f, 0.1f);

        if (AITimer >= screenRumbleTime + roarTime)
            SelectNextAttack();
    }

    public void DoBehavior_RedirectingDisgustingStars()
    {
        int armAnimationTime = 32;
        int starReleaseRate = 3;
        int armSlamDelay = 10;
        int ropeDangleTime = 120;
        ref float offsetAngle = ref NPC.ai[3];

        if (AITimer <= 2)
            Arms.Clear();

        // Create arms on the first frame.
        if (AITimer % 30f == 0 && AITimer <= 90f)
        {
            while (Arms.Count > 6)
                Arms.RemoveAt(0);

            for (int i = 0; i < 2; i++)
                Arms.Add(new(NPC.Center, Vector2.Zero));

            NPC.netUpdate = true;
        }

        // Hover above the player.
        if (AITimer >= armAnimationTime)
            NPC.velocity *= 0.9f;
        else
            NPC.SmoothFlyNearWithSlowdownRadius(Target.Center - Vector2.UnitY * 450f, 0.1f, 0.6f, 150f);

        // Disable contact damage.
        NPC.damage = 0;

        // Update arms.
        for (int i = 0; i < Arms.Count; i++)
        {
            // Make arms initially raise themselves in anticipation of summoning the festooned stars from above.
            bool left = i % 2 == 0;
            float armAnimationCompletion = InverseLerp(0f, armAnimationTime, Arms[i].Time);
            float horizontalOffset = left.ToDirectionInt() * Lerp(760f, 1100f, Convert01To010(armAnimationCompletion)) * Utils.Remap(armAnimationCompletion, 0.65f, 1f, 1f, 0.2f);
            float verticalOffset = Lerp(Pow(armAnimationCompletion, 0.4f) * 2100f, -560f, armAnimationCompletion);
            float armSlamInterpolant = InverseLerp(0f, 12f, AITimer - armAnimationTime - armSlamDelay - ropeDangleTime);
            horizontalOffset += left.ToDirectionInt() * Convert01To010(armSlamInterpolant) * 600f;
            verticalOffset += armSlamInterpolant.Squared() * 1750f;

            Vector2 offset = Vector2.Lerp(new(horizontalOffset, verticalOffset), new(horizontalOffset * 1.5f, verticalOffset), armAnimationCompletion);

            Arms[i].Scale = NPC.scale * InverseLerp(240f, 210f, AITimer).Squared() * 0.75f;
            Arms[i].AnchorOffset = Vector2.UnitX * left.ToDirectionInt() * Arms[i].Scale * 100f;
            Arms[i].Center = Vector2.Lerp(Arms[i].Center, NPC.Center + offset * Arms[i].Scale, 0.136f);
            Arms[i].Time++;
            if (armAnimationCompletion >= 0.85f)
                Arms[i].Center += NPC.SafeDirectionTo(Arms[i].Center).RotatedBy(Sin(Arms[i].Time / 8f) * 1.01f) * 18f;

            Arms[i].VerticalFlip = left;
        }

        // Play incredibly gross sounds as the stars are summoned.
        if (AITimer == (int)(armAnimationTime * 0.4f) + 30)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.DisgustingStarSummon with { Volume = 1.2f, MaxInstances = 10 });

        // Release stars during the animation.
        if (AITimer >= armAnimationTime * 0.4f && AITimer <= armAnimationTime && AITimer % starReleaseRate == 0f)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (offsetAngle == 0f)
                    offsetAngle = Main.rand.NextFloat(TwoPi);

                float starHorizontalSummonOffset = Utils.Remap(AITimer, armAnimationTime * 0.4f, armAnimationTime - 1f, -1200f, 1200f) + Main.rand.NextFloatDirection() * 90f;

                // Prevent stars from being too close to the player at the start.
                if (Abs(starHorizontalSummonOffset) <= 275f)
                    starHorizontalSummonOffset = Sign(starHorizontalSummonOffset) * Main.rand.NextFloat(275f, 350f);

                NewProjectileBetter(NPC.GetSource_FromAI(), Target.Center + new Vector2(starHorizontalSummonOffset, -900f), Vector2.Zero, ModContent.ProjectileType<DisgustingStar>(), DisgustingStarDamage, 0f, -1, starHorizontalSummonOffset, Pi * starHorizontalSummonOffset / 1200f + offsetAngle);
            }
        }

        if (AITimer == armAnimationTime + armSlamDelay + ropeDangleTime)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.DisgustingStarSever with { Volume = 1.2f, MaxInstances = 10 });
            CustomScreenShakeSystem.Start(30, 6f);

            BloodMetaball metaball = ModContent.GetInstance<BloodMetaball>();
            foreach (Projectile star in AllProjectilesByID(ModContent.ProjectileType<DisgustingStar>()))
            {
                star.As<DisgustingStar>().DanglingFromTop = false;
                star.As<DisgustingStar>().DanglingRope.Rope[^2].Position += Main.rand.NextVector2CircularEdge(2600f, 600f) - Vector2.UnitY * 1900f;
                star.velocity = star.SafeDirectionTo(Target.Center) * 4f;
                star.netUpdate = true;

                for (int i = 0; i < 8; i++)
                    metaball.CreateParticle(star.Center - Vector2.UnitY * 46f, Main.rand.NextVector2Circular(9f, 9f) - Vector2.UnitY * Main.rand.NextFloat(4f, 10f), Main.rand.NextFloat(35f, 45f));
            }
        }

        if (AITimer >= 240f)
            SelectNextAttack();

        // Slow down.
        NPC.velocity *= 0.9f;
    }

    public void DoBehavior_ReleaseOtherworldlyThorns()
    {
        int teleportDelay = 30;
        int teleportAnimationTime = 8;
        int teleportDisappearTime = 4;
        int portalSummonTime = 60;
        int portalSummonRate = 3;
        int portalLifetime = (int)(OtherworldlyThorn.Lifetime * 1.02f);
        int postTeleportTimer = (int)AITimer - (teleportDelay + teleportAnimationTime * 2 + teleportDisappearTime);
        int attackTransitionDelay = 136;
        int armCount = 5;
        float portalScale = 0.73f;
        float portalCoverage = GetAIFloat("ReleaseOtherworldlyThorns_PortalCoverage");
        float portalSummonInterpolant = InverseLerp(0f, portalSummonTime, postTeleportTimer);
        ref float startingX = ref NPC.ai[3];

        // Disappear at first.
        NPC.scale = 1f - InverseLerpBump(0f, teleportAnimationTime, teleportAnimationTime + teleportDisappearTime, teleportAnimationTime * 2f + teleportDisappearTime, AITimer - teleportDelay);
        NPC.Opacity = (NPC.scale >= 0.1f).ToInt();

        // Disable contact damage.
        NPC.damage = 0;

        // Teleport near the player at first.
        if (AITimer == teleportDelay + teleportAnimationTime)
        {
            CreateTwinkle(NPC.Center, Vector2.One * 5.6f, false);
            TeleportTo(Target.Center + new Vector2(NPC.HorizontalDirectionTo(Target.Center) * 650f, -300f));
        }

        // Create arms on the first frame.
        if (Arms.Count < armCount && AITimer <= teleportDelay)
        {
            Arms.Clear();

            for (int i = 0; i < armCount; i++)
                Arms.Add(new(NPC.Center, Vector2.Zero));

            NPC.netUpdate = true;
        }

        // Hover near the player after teleporting.
        if (postTeleportTimer >= 1 && postTeleportTimer % 60f < 30f)
        {
            float flySpeedInterpolant = InverseLerp(0f, 20f, postTeleportTimer % 60f) * 0.09f;
            NPC.SmoothFlyNearWithSlowdownRadius(Target.Center - Vector2.UnitY * 400f, flySpeedInterpolant, 1f - flySpeedInterpolant * 2f, 100f);
        }
        else
            NPC.velocity *= 0.8f;

        float armExtendInterpolant = InverseLerp(-60f, -27f, postTeleportTimer - portalLifetime * 0.48f);
        float armReboundInterpolant = InverseLerp(14f, 31f, postTeleportTimer - portalLifetime * 0.48f);
        float armRotateInterpolant = InverseLerp(30f, 78f, postTeleportTimer - portalLifetime * 0.48f);
        for (int i = 0; i < Arms.Count; i++)
        {
            float armDisappearInterpolant = InverseLerp(attackTransitionDelay - i * 8f - 14f, attackTransitionDelay, postTeleportTimer).Squared();

            // Calculate the arm's extend distance.
            // This is affected by the rebound interpolate, assuming the arm isn't in the process of disappearing.
            // If it is disappearing, that means that the arm should straighten out as it does so, hence the cancellation.
            float armExtendDistance = Pow(armExtendInterpolant, 4f) * 1100f - Pow(armReboundInterpolant, 1.9f) * (1f - armDisappearInterpolant).Cubed() * 500f;

            // Make arms initially raise themselves in anticipation of summoning the festooned stars from above.
            Vector2 armDirection = ((TwoPi * i / Arms.Count).ToRotationVector2() - Vector2.UnitY * 0.9f).SafeNormalize(Vector2.UnitY);
            armDirection = armDirection.RotatedBy(PiOver2 * armDirection.X.NonZeroSign() * armReboundInterpolant * 0.7f);

            Vector2 armHoverOffset = armDirection * armExtendDistance + Main.rand.NextVector2CircularEdge(20f, 20f);
            Vector2 anchorOffsetDirection = armDirection.RotatedBy(Pi * armDirection.X.NonZeroSign() * armRotateInterpolant * 1.75f);

            Arms[i].Scale = NPC.scale * armExtendInterpolant * (1f - armDisappearInterpolant) * 0.65f;
            Arms[i].AnchorOffset = anchorOffsetDirection * Arms[i].Scale * 100f;
            Arms[i].Center = Vector2.Lerp(Arms[i].Center, NPC.Center + armHoverOffset * Arms[i].Scale, 0.5f);
            Arms[i].VerticalFlip = armDirection.X < 0f;
            Arms[i].FlipHandDirection = true;
            Arms[i].HandRotationAngularOffset = Pi + armDirection.X.NonZeroSign() * armReboundInterpolant;
        }

        // Summon dark portals below the player.
        if (portalSummonInterpolant > 0f && portalSummonInterpolant < 1f && postTeleportTimer % portalSummonRate == 0)
        {
            if (startingX == 0f)
            {
                startingX = Target.Center.X;
                NPC.netUpdate = true;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int portalSummonCounter = postTeleportTimer / portalSummonRate;
                float horizontalSummonOffset = (Lerp(-180f, portalCoverage, Pow(portalSummonInterpolant, 0.85f)) + Main.rand.NextFloat(50f)) * (portalSummonCounter % 2 == 0).ToDirectionInt();
                Vector2 portalSummonPosition = new Vector2(startingX, Target.Center.Y) + new Vector2(horizontalSummonOffset, 540f);

                NewProjectileBetter(NPC.GetSource_FromAI(), portalSummonPosition, -Vector2.UnitY.RotatedByRandom(0.12f), ModContent.ProjectileType<DarkPortal>(), 0, 0f, -1, portalScale, portalLifetime, (int)DarkPortal.PortalAttackAction.ReleaseOtherworldlyThorns);
            }
        }

        if (postTeleportTimer >= attackTransitionDelay)
            SelectNextAttack();
    }

    // Refer to AvatarRiftSuckVisualsManager.cs in the SpecificEffectManagers folder for details on how the visuals for this work.
    public void DoBehavior_SuckPlayerIn()
    {
        int teleportDelay = 30;
        int teleportAnimationTime = 9;
        int teleportDisappearTime = 3;
        int postTeleportTimer = (int)AITimer - (teleportDelay + teleportAnimationTime * 2 + teleportDisappearTime);
        int suckTime = 420;
        int rubbleReleaseRate = 15;
        float rubbleSpawnOffset = 1400f;
        float suckCompletion = InverseLerp(0f, suckTime, postTeleportTimer);
        bool allPlayersHaveBeenSuckedUp = Main.player.Where(p => p.active && !p.dead).All(p => p.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName));
        ref float attackContinuationDelay = ref NPC.ai[3];

        SolynAction = s => StandardSolynBehavior_FlyNearPlayer(s, NPC);

        // Disable contact damage. Damage is applied manually in other contexts.
        NPC.damage = 0;

        // If necessary, delay the attack from continuing so that players can spend a minimum amount of time in the the Avatar's Dimension.
        if (attackContinuationDelay > 0f)
        {
            AITimer--;
            attackContinuationDelay--;
        }

        // Make the attack go by faster if all players are sucked up.
        else if (SuckOpacity >= 0.2f && allPlayersHaveBeenSuckedUp)
            AITimer++;

        // Disappear at first.
        NPC.scale = 1f - InverseLerpBump(0f, teleportAnimationTime, teleportAnimationTime + teleportDisappearTime, teleportAnimationTime * 2f + teleportDisappearTime, AITimer - teleportDelay);
        NPC.velocity *= 0.8f;
        NPC.Opacity = (NPC.scale >= 0.1f).ToInt();

        Arms.Clear();

        // Open eyes.
        float blinkFactor = InverseLerp(0.925f, 0.8f, (Cos01(TwoPi * AITimer / 150f) + Cos01(MathF.E * AITimer / 60f)) * 0.5f);
        EyesOpacity = InverseLerp(0f, 30f, AITimer - teleportDelay - teleportAnimationTime) * blinkFactor;
        if (Main.rand.NextBool(40))
            EyesDrawOffsetDestination = Main.rand.NextVector2Circular(100f, 50f);

        // Teleport near the player at first.
        if (AITimer == teleportDelay + teleportAnimationTime)
        {
            CreateTwinkle(NPC.Center, Vector2.One * 5.6f, false);

            // Destroy arms.
            Arms.Clear();

            TeleportTo(Target.Center + new Vector2(Target.direction * -475f, -160f));
        }

        // Chirp shortly after the teleport.
        if (AITimer == teleportDelay + teleportAnimationTime + 10f)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Chirp with { Volume = 4f });

        // Make the suction increase gradually in pitch.
        SuckSoundPitch = suckCompletion * 0.37f - InverseLerp(0.35f, 0f, suckCompletion) * 0.6f;

        // Suck the player and various large objects inward.
        if (suckCompletion > 0f && suckCompletion < 1f)
        {
            float strongSuckDistanceThreshold = GetAIFloat("SuckPlayerIn_StrongSuctionDistanceThreshold");
            float weakSuckDistanceThreshold = GetAIFloat("SuckPlayerIn_WeakSuctionDistanceThreshold");
            float weakAccelerationFactor = GetAIFloat("SuckPlayerIn_WeakSuctionAccelerationFactor");
            float suckPower = InverseLerpBump(0f, 0.65f, 0.93f, 1f, suckCompletion);
            float suckAcceleration = InverseLerp(0f, 0.25f, suckCompletion) * GetAIFloat("SuckPlayerIn_MaxSuctionAcceleration");
            float suckDistanceThreshold = 12000f;
            float eventHorizonThreshold = 200f;

            // Move incredibly slowly towards the player.
            NPC.Center = NPC.Center.MoveTowards(Target.Center, suckPower * 3f);

            // Make players approach the rift based on how far away they are.
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                float distanceToPlayer = Main.player[i].Distance(NPC.Center);
                if (distanceToPlayer < suckDistanceThreshold && Main.player[i].grappling[0] == -1)
                {
                    // Disable mounts.
                    Main.player[i].mount?.Dismount(Main.player[i]);

                    float suckDirectionX = (Main.player[i].Center.X < NPC.Center.X).ToDirectionInt();
                    float suckDirectionY = (Main.player[i].Center.Y < NPC.Center.Y).ToDirectionInt();
                    float eventHorizonInterpolant = InverseLerp(eventHorizonThreshold + 100f, eventHorizonThreshold, Main.player[i].Distance(NPC.Center));
                    float distanceTaperOff = Lerp(weakAccelerationFactor, 1f, InverseLerp(760f, 1850f, distanceToPlayer)) + eventHorizonInterpolant * 3f;

                    // Apply suck forces to the player's velocity.
                    Main.player[i].velocity.X += suckAcceleration * distanceTaperOff * suckDirectionX;
                    if (Abs(Main.player[i].velocity.Y) >= 0.001f)
                        Main.player[i].velocity.Y += suckAcceleration * distanceTaperOff * suckDirectionY * 0.64f;

                    // Make the player enter the the Avatar's Dimension if hit.
                    if (distanceToPlayer <= NPC.width * NPC.scale * 0.11f && suckCompletion < 0.9f && !Main.player[i].GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName).Value)
                    {
                        if (Main.myPlayer == i)
                            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftPlayerAbsorb);
                        Main.player[i].GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName).Value = true;
                        Main.player[i].GetValueRef<float>(AvatarRiftSuckVisualsManager.ZoomInInterpolantName).Value = 0.001f;
                        PacketManager.SendPacket<PlayerAvatarRiftStatePacket>(i);

                        // Re-evaluate if all players are in the portal.
                        // If they are, check to see if the attack transition needs to be delayed so that the animation lingers for a sufficient amount of time.
                        allPlayersHaveBeenSuckedUp = Main.player.Where(p => p.active && !p.dead).All(p => p.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName));
                        if (allPlayersHaveBeenSuckedUp)
                        {
                            int timeUntilAttackEnds = (int)(suckTime * 0.9f) - postTeleportTimer;
                            if (timeUntilAttackEnds < 150)
                            {
                                attackContinuationDelay = 150 - timeUntilAttackEnds;
                                NPC.netUpdate = true;
                            }
                        }

                        if (Main.myPlayer == i)
                            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 1f, 60);
                    }

                    if (suckCompletion < 0.9f && SuckOpacity >= 0.8f && Main.player[i].GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName))
                    {
                        Main.player[i].Center = NPC.Center;
                        Main.player[i].velocity.Y = -0.1f;

                        if (i == Main.myPlayer)
                            BlockerSystem.Start(true, true, () => Main.LocalPlayer.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName));
                    }
                }
            }

            // Make the suck effect appear.
            SuckOpacity = InverseLerp(0f, 0.16f, suckPower) * InverseLerp(1f, 0.9f, suckCompletion);
        }

        // Release rubble.
        if (Main.netMode != NetmodeID.MultiplayerClient && suckCompletion < 0.8f && AITimer % rubbleReleaseRate == rubbleReleaseRate - 1f && postTeleportTimer >= 45f)
        {
            Vector2 directionToTarget = NPC.SafeDirectionTo(Target.Center);
            Vector2 rubbleSpawnPosition = Target.Center + directionToTarget.RotatedByRandom(0.74f) * rubbleSpawnOffset;
            Vector2 rubbleVelocity = (NPC.Center - rubbleSpawnPosition).SafeNormalize(Vector2.UnitY) * 4f;
            NewProjectileBetter(NPC.GetSource_FromAI(), rubbleSpawnPosition, rubbleVelocity, ModContent.ProjectileType<AcceleratingRubble>(), RubbleDamage, 0f);
        }

        // Eject the player from the portal and do damage to them.
        if (suckCompletion >= 0.9f)
            EjectPlayerFromVisualsDimension();

        if (suckCompletion >= 1f)
            SelectNextAttack();
    }

    public void DoBehavior_PlasmaBlasts()
    {
        int teleportDelay = 9;
        int teleportAnimationTime = 13;
        int teleportDisappearTime = 3;
        int paleCometCount = 13;
        int postTeleportTimer = (int)AITimer - (teleportDelay + teleportAnimationTime * 2 + teleportDisappearTime);
        int shootDelay = 78;
        int attackTransitionDelay = shootDelay + 90;
        int gasShootCount = 16;
        float paleCometSpread = ToRadians(82f);
        float paleCometShootSpeed = 4.75f;

        // Disappear at first.
        NPC.scale = 1f - InverseLerpBump(0f, teleportAnimationTime, teleportAnimationTime + teleportDisappearTime, teleportAnimationTime * 2f + teleportDisappearTime, AITimer - teleportDelay);
        NPC.Opacity = (NPC.scale >= 0.1f).ToInt();

        // Disable contact damage.
        NPC.damage = 0;

        // Suck in a bit before comets are released.
        SuckOpacity = Convert01To010(InverseLerp(0f, shootDelay, postTeleportTimer)).Cubed();
        SuckSoundVolumeOverride = 0f;
        if (SuckOpacity > 0f)
            ScreenShakeSystem.SetUniversalRumble(SuckOpacity * 5f, TwoPi, null, 0.2f);
        if (postTeleportTimer == 1)
        {
            SlotId soundSlot = SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Inhale, Vector2.Lerp(NPC.Center, Main.LocalPlayer.Center, 0.5f));
            if (SoundEngine.TryGetActiveSound(soundSlot, out ActiveSound? sound))
                sound.Volume *= 3.2f;
        }

        if (postTeleportTimer < shootDelay / 2)
            RiftRotationSpeedInterpolant = Lerp(RiftRotationSpeedInterpolant, SuckOpacity, 0.08f);

        // Teleport near the player at first.
        if (AITimer == teleportDelay + teleportAnimationTime)
        {
            CreateTwinkle(NPC.Center, Vector2.One * 5.6f, false);

            // Destroy arms.
            Arms.Clear();

            TeleportTo(Target.Center + new Vector2(Target.direction * 700f + Target.velocity.X * 32f, -100f));
        }

        // Release the comets.
        if (postTeleportTimer == shootDelay)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftEject, Target.Center);
            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 0.7f, 12);
            CustomScreenShakeSystem.Start(55, 11f).
                WithDistanceFadeoff(NPC.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<DarkWave>(), 0, 0f, -1, 0f, 0f, 1f);

            // Shoot the comets.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 cometSpawnPosition = NPC.Center;
                for (int i = 0; i < paleCometCount; i++)
                {
                    float localPaleCometSpread = Lerp(-paleCometSpread, paleCometSpread, i / (float)(paleCometCount - 1f));
                    Vector2 paleCometShootVelocity = NPC.SafeDirectionTo(Target.Center).RotatedBy(localPaleCometSpread) * paleCometShootSpeed;
                    NewProjectileBetter(NPC.GetSource_FromAI(), cometSpawnPosition, paleCometShootVelocity, ModContent.ProjectileType<PaleComet>(), NPC.defDamage, 0f);
                }

                for (int i = 0; i < gasShootCount; i++)
                {
                    float localGasSpread = Main.rand.NextFloatDirection() * paleCometSpread * 0.5f;
                    Vector2 paleCometShootVelocity = NPC.SafeDirectionTo(Target.Center).RotatedBy(localGasSpread) * paleCometShootSpeed * Main.rand.NextFloat(1.7f, 2.5f);
                    NewProjectileBetter(NPC.GetSource_FromAI(), cometSpawnPosition, paleCometShootVelocity, ModContent.ProjectileType<DarkGas>(), NPC.defDamage, 0f);
                }
            }
        }

        if (postTeleportTimer >= attackTransitionDelay)
            SelectNextAttack();
    }

    public void DoBehavior_FlailingFrenzy()
    {
        int armSpawnRate = 6;
        int maxArms = 8;
        int portalSummonDelay = 76;
        int portalSummonRate = 8;
        int portalLifetime = 116;
        int flailTime = 180;
        float portalScale = 0.75f;

        // Flail arms.
        FlailArms(armSpawnRate, maxArms, true, InverseLerp(0f, 24f, AITimer) * 0.85f);

        // Hover near the player.
        Vector2 hoverDestination = Target.Center + new Vector2((NPC.Center.X - Target.Center.X).NonZeroSign() * 210f, -100f);
        NPC.velocity += NPC.SafeDirectionTo(hoverDestination) * 0.4f;
        NPC.velocity = NPC.velocity.ClampLength(0f, 20f);

        // Roar at first.
        if (AITimer % 10f == 0f && AITimer <= 30f)
            AttachedSounds.Add(SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Angry with { MaxInstances = 10 }, NPC.Center));

        // Summon portals around the player.
        if (AITimer >= portalSummonDelay && AITimer % portalSummonRate == portalSummonRate - 1f)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                float portalSpawnOffset = Utils.Remap(Target.velocity.Length(), 3f, 25f, Main.rand.NextFloat(150f, 200f), 0f);
                Vector2 portalSpawnPosition = Target.Center + Target.velocity.SafeNormalize(Main.rand.NextVector2Unit()) * portalSpawnOffset + Target.velocity * 51f + Main.rand.NextVector2Circular(60f, 60f);
                Vector2 portalDirection = (Target.Center - portalSpawnPosition).SafeNormalize(Vector2.UnitY);
                NewProjectileBetter(NPC.GetSource_FromAI(), portalSpawnPosition, portalDirection * 4f, ModContent.ProjectileType<DarkPortal>(), 0, 0f, -1, portalScale, portalLifetime, (int)DarkPortal.PortalAttackAction.StrikingArm);
            }
        }

        if (AITimer >= portalSummonDelay + flailTime)
            SelectNextAttack();
    }

    public void DoBehavior_TrembleInPlace()
    {
        int teleportDelay = 9;
        int teleportAnimationTime = 12;
        int teleportDisappearTime = 3;
        int attackTransitionDelay = 96;
        int postTeleportTimer = (int)AITimer - (teleportDelay + teleportAnimationTime * 2 + teleportDisappearTime);

        // Disappear at first.
        NPC.scale = 1f - InverseLerpBump(0f, teleportAnimationTime, teleportAnimationTime + teleportDisappearTime, teleportAnimationTime * 2f + teleportDisappearTime, AITimer - teleportDelay);
        NPC.Opacity = (NPC.scale >= 0.1f).ToInt();

        // Disable contact damage.
        NPC.damage = 0;

        // Teleport near the player at first.
        if (AITimer == teleportDelay + teleportAnimationTime)
        {
            CreateTwinkle(NPC.Center, Vector2.One * 5.6f, false);

            // Create two arms.
            Arms.Clear();
            for (int i = 0; i < 4; i++)
                Arms.Add(new(NPC.Center, Vector2.Zero));

            TeleportTo(Target.Center + new Vector2(Target.direction * 420f, -410f));
        }

        // Update arms, having them jitter in place.
        float animationCompletion = InverseLerp(0f, attackTransitionDelay, postTeleportTimer);
        float jitterSpeed = animationCompletion.Squared() * 60f;
        for (int i = 0; i < Arms.Count; i++)
        {
            bool left = i % 2 == 0;
            Vector2 offset = new Vector2(left.ToDirectionInt() * (320f + i / 2 * 90f), -210f + i / 2 * 80f - animationCompletion * 50f);
            Arms[i].Scale = NPC.scale * InverseLerp(attackTransitionDelay, attackTransitionDelay - 15f, postTeleportTimer) * 0.75f;
            Arms[i].AnchorOffset = new Vector2(left.ToDirectionInt() * 100f, 90f) * Arms[i].Scale;
            Arms[i].Center = Vector2.Lerp(Arms[i].Center, NPC.Center + offset, 0.4f) + Main.rand.NextVector2Circular(jitterSpeed, jitterSpeed);
            Arms[i].VerticalFlip = left ^ (offset.Y > 0f);
            Arms[i].FlipHandDirection = true;
            Arms[i].HandRotationAngularOffset = Pi;
        }

        // Murmur after the teleport.
        if (postTeleportTimer == 10f)
            AttachedSounds.Add(SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Murmur with { Volume = 3f }, NPC.Center));

        // Hover in place, with slight jittering.
        if (postTeleportTimer >= 1)
        {
            NPC.velocity = Vector2.UnitY * Sin(TwoPi * postTeleportTimer / 60f) * 6f;
            NPC.position += Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
        }
        else
            NPC.velocity *= 0.8f;

        // Resume attacking.
        if (postTeleportTimer >= attackTransitionDelay)
        {
            CurrentAttack = RiftAttackType.FlailingFrenzy;
            AITimer = 0f;
            NPC.netUpdate = true;
        }
    }

    public void DoBehavior_KillOldDuke()
    {
        // Teleport behind the Old Duke.
        NPC.scale = InverseLerp(0f, 6f, AITimer);

        // Disable music.
        Music = 0;

        // Draw behind the Old Duke.
        NPC.hide = true;

        // Create teleport visuals and sounds on the first frame.
        if (AITimer == 1f)
            TeleportTo(NPC.Center);

        // Hide the HP bar.
        HideBar = true;

        // Flail arms.
        if (AITimer >= FUCKYOUOLDDUKESystem.AttackDelay && AITimer < FUCKYOUOLDDUKESystem.AttackDelay + FUCKYOUOLDDUKESystem.AvatarAttackTime)
        {
            FlailArms(3, 10, false, 0f);

            // Play angry sounds at the sight of Old Duke.
            if (AITimer <= FUCKYOUOLDDUKESystem.AttackDelay + 2f)
                AttachedSounds.Add(SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Angry with { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew }).WithVolumeBoost(1.5f));
        }

        // Get rid of arms and leave.
        if (AITimer >= FUCKYOUOLDDUKESystem.AttackDelay + FUCKYOUOLDDUKESystem.AvatarAttackTime)
        {
            float leaveInterpolant = InverseLerp(15f, 0f, AITimer - FUCKYOUOLDDUKESystem.AttackDelay - FUCKYOUOLDDUKESystem.AvatarAttackTime);
            NPC.scale *= leaveInterpolant;
            NPC.Opacity *= leaveInterpolant;
            if (leaveInterpolant > 0f)
                Arms.Clear();

            // Leave.
            if (AITimer == FUCKYOUOLDDUKESystem.AttackDelay + FUCKYOUOLDDUKESystem.AvatarAttackTime + 15f)
            {
                CreateTwinkle(NPC.Center, Vector2.One * 5f);
                NPC.Center = Target.Center + Vector2.UnitY * 20000f;
            }

            // Kill Old Duke.
            if (AITimer == FUCKYOUOLDDUKESystem.AttackDelay + FUCKYOUOLDDUKESystem.AvatarAttackTime + FUCKYOUOLDDUKESystem.LootDelay)
            {
                NPC dummyOldDuke = new NPC();
                dummyOldDuke.SetDefaults(FUCKYOUOLDDUKESystem.OldDukeID);
                dummyOldDuke.Center = Target.Center - Vector2.UnitY * 900f;
                if (dummyOldDuke.position.Y < 400f)
                    dummyOldDuke.position.Y = 400f;

                // Ensure that the loot does not appear in the middle of a bunch of blocks.
                for (int i = 0; i < 600; i++)
                {
                    if (!Collision.SolidCollision(dummyOldDuke.Center, 1, 1))
                        break;

                    dummyOldDuke.position.Y++;
                }

                // Log a kill in the bestiary.
                Main.BestiaryTracker.Kills.RegisterKill(dummyOldDuke);

                // Drop Old Duke's mutilated corpse.
                dummyOldDuke.life = 0;
                dummyOldDuke.HitEffect();

                // Give the player Old Duke's loot and mark him as defeated if Polterghast has been defeated.
                if (CommonCalamityVariables.PolterghastDefeated)
                {
                    dummyOldDuke.NPCLoot();
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        dummyOldDuke.ModNPC.OnKill();
                }
            }

            // Ensure that the Avatar's scale has to increase again when he returns to the sky after killing Old Duke.
            RiftEclipseManagementSystem.RiftScale = 0f;

            if (AITimer >= FUCKYOUOLDDUKESystem.AttackDelay + FUCKYOUOLDDUKESystem.AvatarAttackTime + FUCKYOUOLDDUKESystem.LootDelay)
                NPC.active = false;
        }

        // Play murmur sounds.
        if (AITimer == 1)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Murmur with { MaxInstances = 0 }).WithVolumeBoost(1.75f);
    }

    public void DoBehavior_DeathAnimation()
    {
        // Eject players from the dimension.
        EjectPlayerFromVisualsDimension();

        // Die.
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            // Register the first form as defeated in the bestiary.
            Main.BestiaryTracker.Kills.RegisterKill(NPC);

            // Spawn the Entropic God.
            NPC.NewNPC(NPC.GetSource_Death(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<AvatarOfEmptiness>(), 1);

            // Create a wave effect.
            NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<DarkWave>(), 0, 0f, -1, 0f, 0f, 1f);
        }

        // Ensure that the suction effect immediately vanishes, in case he was doing that attack when transitioning.
        if (Main.netMode != NetmodeID.Server)
        {
            ManagedScreenFilter spaghettificationShader = ShaderManager.GetFilter("NoxusBoss.AvatarRiftSpaghettificationShader");
            ManagedScreenFilter suctionShader = ShaderManager.GetFilter("NoxusBoss.SuctionSpiralShader");
            spaghettificationShader.Deactivate();
            suctionShader.Deactivate();
            for (int i = 0; i < 50; i++)
            {
                spaghettificationShader.Update();
                suctionShader.Update();
            }
        }

        NPC.life = 0;
        NPC.checkDead();

        BossDownedSaveSystem.SetDefeatState<AvatarRift>(true);
        NPC.active = false;
    }

    public void EjectPlayerFromVisualsDimension()
    {
        if (!AvatarRiftSuckVisualsManager.WasSuckedIntoNoxusPortal)
            return;

        foreach (Player player in Main.ActivePlayers)
        {
            if (!player.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName))
                continue;

            // Create teleport visual and acoustic effects.
            player.GetValueRef<bool>(AvatarRiftSuckVisualsManager.WasSuckedInVariableName).Value = false;
            PacketManager.SendPacket<PlayerAvatarRiftStatePacket>(player.whoAmI);

            SoundEngine.StopTrackedSounds();
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.NamelessVortexLoopEnd with { Volume = 2f });

            // Hurt the player as they're ejected.
            player.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), Main.rand.Next(500, 600), 0);

            // Shove the player out.
            player.velocity.X = player.direction * 50f;

            // Overlay the screen with black.
            if (Main.myPlayer == player.whoAmI)
            {
                TotalScreenOverlaySystem.OverlayColor = Color.Black;
                TotalScreenOverlaySystem.OverlayInterpolant = 1.2f;
            }
        }
    }

    public void FlailArms(int armSpawnRate, int maxArms, bool armsDoDamage, float lookAtPlayerInterpolantFactor)
    {
        // Constantly create arms.
        if (AITimer % armSpawnRate == armSpawnRate - 1f && Arms.Count < maxArms)
        {
            // Create a new arm.
            AvatarShadowArm arm = new AvatarShadowArm(NPC.Center, Vector2.Zero, armsDoDamage)
            {
                Scale = Main.rand.NextFloat(0.6f, 1.95f)
            };
            Arms.Add(arm);
        }

        // Update all arms.
        for (int i = 0; i < Arms.Count; i++)
        {
            ulong seed = unchecked((ulong)Arms[i].RandomID);

            // Make arms flail in place based on aperiodic sinusoidal curves. This provides rapidly changing variance over both the angle and outward stretch of the arms.
            // As arms increase this results in a grotesque, horrifying amalgamation all vaguely flailing about in the general direction of the player.
            float baseArmAngleOffset = (Utils.RandomFloat(ref seed) * 12f).AngleLerp(NPC.AngleTo(Target.Center), lookAtPlayerInterpolantFactor);
            float animationCompletion = Arms[i].Time / 28f;
            float swingDirection = (Arms[i].RandomID % 2 == 0).ToDirectionInt();
            float armAngleOffset = baseArmAngleOffset + SmoothStep(0f, PiOver2 * swingDirection, animationCompletion.Squared());

            float armOffsetLength = 1000f;
            Vector2 armDirection = armAngleOffset.ToRotationVector2();
            Vector2 armOffset = armDirection * armOffsetLength;

            Arms[i].Scale = NPC.scale * InverseLerpBump(0f, 0.35f, 0.5f, 1f, animationCompletion) * 0.62f;
            Arms[i].Center = Vector2.Lerp(Arms[i].Center, NPC.Center + armOffset * NPC.scale * Arms[i].Scale, 0.32f);
            Arms[i].AnchorOffset = (armAngleOffset * -2f).ToRotationVector2() * Arms[i].Scale * 150f;
            Arms[i].FlipHandDirection = swingDirection > 0f;
            Arms[i].HandRotationAngularOffset = Arms[i].FlipHandDirection ? Pi : 0f;
            Arms[i].Time++;

            if (animationCompletion >= 1f)
            {
                Arms.RemoveAt(i);
                i--;
            }
        }
    }

    public void UpdateAmbientSounds()
    {
        // Initialize the ambience loop if necessary.
        if ((AmbienceLoop is null || AmbienceLoop.HasBeenStopped) && !AvatarRiftSuckVisualsManager.WasSuckedIntoNoxusPortal)
        {
            AmbienceLoop = LoopedSoundManager.CreateNew(GennedAssets.Sounds.Avatar.RiftAmbientLoop, () =>
            {
                return !NPC.active || AvatarRiftSuckVisualsManager.WasSuckedIntoNoxusPortal;
            });
        }

        // Update the ambience loop.
        AmbienceLoop?.Update(NPC.Center, sound =>
        {
            // Determine if the scale is expanding based on whether it has increased since the last frame.
            bool expanding = NPC.scale >= PreviousScale;

            // Pitch goes down when collapsing, and goes up when expanding.
            // Near the end of an expansion it stabilizes back to zero again.
            float scalePitch = InverseLerp(-1f, 0f, NPC.scale);
            if (expanding)
                scalePitch = InverseLerpBump(0f, 0.6f, 0.7f, 1f, NPC.scale);
            float pitch = Clamp(scalePitch + NPC.velocity.Length() * 0.0049f, -1f, 1f);

            if (Distance(sound.Pitch, pitch) >= 0.01f)
                sound.Pitch = pitch;
            sound.Volume = CurrentAttack == RiftAttackType.Awaken ? 0f : 0.25f;
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
            sound.Volume = SuckSoundVolumeOverride ?? (SuckOpacity * 2.95f + 1e-5f);

            /* Demonic levels of lag.
            if (Distance(sound.Pitch, SuckSoundPitch) >= 0.04f)
                sound.Pitch = SuckSoundPitch;
            */
        });
    }

    public void UpdateAttachedSounds()
    {
        List<ActiveSound> validSounds = [];

        for (int i = 0; i < AttachedSounds.Count; i++)
        {
            if (SoundEngine.TryGetActiveSound(AttachedSounds[i], out ActiveSound? sound))
                validSounds.Add(sound);

            // If this sound is no longer registered, remove it and restart the loop.
            else
            {
                AttachedSounds.RemoveAt(i);
                i = 0;
            }
        }

        // Attach all valid sounds to the Avatar's position.
        foreach (ActiveSound sound in validSounds)
            sound.Position = NPC.Center;
    }

    public void SelectNextAttack()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        ResetAttackQueueIfNecessary();
        CurrentAttack = AttackQueue.Dequeue();

        NPC.ai[3] = 0f;
        AITimer = 0f;
        NPC.netUpdate = true;
    }

    public void ResetAttackQueueIfNecessary()
    {
        if (AttackQueue.Count >= 1)
            return;

        List<RiftAttackType[]> possibleAttackSets = new List<RiftAttackType[]>(4)
        {
            new RiftAttackType[] { RiftAttackType.RedirectingDisgustingStars, RiftAttackType.SuckPlayerIn, RiftAttackType.TrembleInPlace },
            new RiftAttackType[] { RiftAttackType.ReleaseOtherworldlyThorns, RiftAttackType.SuckPlayerIn, RiftAttackType.TrembleInPlace },
            new RiftAttackType[] { RiftAttackType.RedirectingDisgustingStars, RiftAttackType.SuckPlayerIn, RiftAttackType.PlasmaBlasts },
            new RiftAttackType[] { RiftAttackType.ReleaseOtherworldlyThorns, RiftAttackType.SuckPlayerIn, RiftAttackType.PlasmaBlasts },
        };
        possibleAttackSets = possibleAttackSets.OrderByDescending(s => Main.rand.NextFloat()).ToList();

        for (int i = 0; i < possibleAttackSets.Count; i++)
        {
            for (int j = 0; j < possibleAttackSets[i].Length; j++)
                AttackQueue.Enqueue(possibleAttackSets[i][j]);
        }
    }

    public void TriggerDeathAnimation()
    {
        SelectNextAttack();
        IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();
        NPC.ai[2] = 1f;
        NPC.dontTakeDamage = true;
        CurrentAttack = RiftAttackType.DeathAnimation;
        NPC.netUpdate = true;
    }

    public void TeleportTo(Vector2 teleportPosition)
    {
        NPC.Center = teleportPosition;
        NPC.velocity = Vector2.Zero;
        NPC.netUpdate = true;

        // Reset the oldPos array, so that afterimages don't suddenly "jump" due to the positional change.
        for (int i = 0; i < NPC.oldPos.Length; i++)
            NPC.oldPos[i] = NPC.position;

        // Play a rift open sound.
        SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftOpen, NPC.Center);

        // Shake the screen.
        CustomScreenShakeSystem.Start(55, 11f);
        GeneralScreenEffectSystem.ChromaticAberration.Start(NPC.Center, 2f, 120);

        // Clear fluids.
        LiquidDrawContents.LiquidPoints.Clear();
    }

    #endregion AI

    #region Drawing

    public override void DrawBehind(int index)
    {
        if (NPC.hide && NPC.Opacity >= 0.02f)
        {
            if (CurrentAttack == RiftAttackType.KillOldDuke)
                Main.instance.DrawCacheNPCsBehindNonSolidTiles.Add(index);
            else
                Main.instance.DrawCacheNPCProjectiles.Add(index);
        }
    }

    public override void BossHeadSlot(ref int index)
    {
        // Make the head icon disappear if the Avatar is invisible.
        if (NPC.Opacity <= 0.45f)
            index = -1;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (SuckOpacity > 0f && !WoTGConfig.Instance.PhotosensitivityMode)
        {
            ManagedScreenFilter spaghettificationShader = ShaderManager.GetFilter("NoxusBoss.AvatarRiftSpaghettificationShader");
            spaghettificationShader.TrySetParameter("distortionRadius", SuckOpacity * 480f);
            spaghettificationShader.TrySetParameter("distortionIntensity", SuckOpacity * (1f - AvatarRiftSuckVisualsManager.ZoomInInterpolant) * 0.8f);
            spaghettificationShader.TrySetParameter("distortionPosition", Vector2.Transform(NPC.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
            spaghettificationShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom.X);
            spaghettificationShader.SetTexture(AvatarRiftTargetManager.AvatarRiftTarget, 1, SamplerState.LinearClamp);
            spaghettificationShader.Activate();

            ManagedScreenFilter suctionShader = ShaderManager.GetFilter("NoxusBoss.SuctionSpiralShader");
            suctionShader.TrySetParameter("suctionCenter", Vector2.Transform(NPC.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix));
            suctionShader.TrySetParameter("zoomedScreenSize", Main.ScreenSize.ToVector2() / Main.GameViewMatrix.Zoom);
            suctionShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom.X);
            suctionShader.TrySetParameter("suctionOpacity", SuckOpacity * (1f - AvatarRiftSuckVisualsManager.ZoomInInterpolant) * 0.32f);
            suctionShader.TrySetParameter("suctionBaseRange", 800f);
            suctionShader.TrySetParameter("suctionFadeOutRange", 500f);
            suctionShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
            suctionShader.Activate();
        }

        if (NPC.IsABestiaryIconDummy || BackgroundProp || DrawnFromTelescope)
            DrawSelf(screenPos);

        return false;
    }

    public void DrawSelf(Vector2 screenPos)
    {
        // Draw the backglow.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, TransformPerspective);
        if (BackgroundProp || DrawnFromTelescope)
            NPC.position -= NPC.Size * 0.5f;

        float backglowScale = NPC.scale * (DrawnFromTelescope ? 0.3f : 0.74f);
        float backglowOpacity = BackgroundProp ? RiftEclipseSky.RiftScaleFactor : 1f;
        Vector2 drawPosition = NPC.Center - screenPos + new Vector2(24f, -120f) * backglowScale;

        if (!DrawnFromTelescope)
        {
            float growInterpolant = RiftEclipseSky.RiftScaleFactor / RiftEclipseSky.ScaleWhenOverSun;
            float growPulse = Convert01To010(growInterpolant.Squared()).Cubed();
            backglowScale += growPulse.Cubed() * Cos01(Main.GlobalTimeWrappedHourly * 56f) * 0.6f + growPulse * 1.3f;
        }

        if (!NPC.IsABestiaryIconDummy)
        {
            if (BackgroundProp && (RiftEclipseBloodMoonRainSystem.EffectActive || RiftEclipseBloodMoonRainSystem.MonolithEffectActive))
            {
                Texture2D flare = Luminance.Assets.MiscTexturesRegistry.ShineFlareTexture.Value;
                Vector2 lensFlareScale = new Vector2(1.5f, 0.48f) * backglowScale * 2f;
                Vector2 lensFlarePosition = NPC.Center - screenPos;

                Main.spriteBatch.Draw(flare, lensFlarePosition, null, NPC.GetAlpha(Color.Red) with { A = 0 } * backglowOpacity, 0f, flare.Size() * 0.5f, lensFlareScale, 0, 0f);
                Main.spriteBatch.Draw(flare, lensFlarePosition, null, NPC.GetAlpha(Color.White) with { A = 0 } * backglowOpacity * 0.6f, 0f, flare.Size() * 0.5f, lensFlareScale * 0.8f, 0, 0f);

                Main.spriteBatch.Draw(BloomCircleSmall, lensFlarePosition, null, NPC.GetAlpha(Color.Red) with { A = 0 } * backglowOpacity, 0f, BloomCircleSmall.Size() * 0.5f, backglowScale * 6f, 0, 0f);
                Main.spriteBatch.Draw(BloomCircleSmall, lensFlarePosition, null, NPC.GetAlpha(Color.Red) with { A = 0 } * backglowOpacity * 0.15f, 0f, BloomCircleSmall.Size() * 0.5f, backglowScale * 36f, 0, 0f);
            }

            Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, NPC.GetAlpha(Color.Red) with { A = 0 } * backglowOpacity * 0.25f, 0f, BloomCircleSmall.Size() * 0.5f, backglowScale * 12.5f, 0, 0f);
            Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, NPC.GetAlpha(Color.Red) with { A = 0 } * backglowOpacity * 0.4f, 0f, BloomCircleSmall.Size() * 0.5f, backglowScale * 6f, 0, 0f);
            Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, NPC.GetAlpha(Color.Crimson) with { A = 0 } * backglowOpacity * 0.12f, 0f, BloomCircleSmall.Size() * 0.5f, backglowScale * 12f, 0, 0f);
        }

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, TransformPerspective);

        // Draw the rift.
        AvatarRiftTargetContent.DrawRiftWithShader(NPC, screenPos, TransformPerspective, BackgroundProp, SuckOpacity, 0f, TargetIdentifierOverride);

        // Draw the eyes.
        float eyesScale = NPC.scale * 1.4f;
        Texture2D eyes = GennedAssets.Textures.SecondPhaseForm.AvatarOfEmptiness.Value;
        Rectangle eyesFrame = eyes.Frame(1, 24, 0, 1);
        eyesFrame.X += 30;
        eyesFrame.Y += 30;
        eyesFrame.Width -= 60;
        eyesFrame.Height -= 60;

        Main.spriteBatch.Draw(eyes, NPC.Center - screenPos + EyesDrawOffset - Vector2.UnitY * NPC.scale * 36f, eyesFrame, Color.White * EyesOpacity, 0f, eyesFrame.Size() * 0.5f, eyesScale, 0, 0f);

        // Draw arms with a special shader.
        DrawArmsWithShader(screenPos);
    }

    public void DrawArms(Vector2 screenPos)
    {
        if (Arms.Count == 0)
            return;

        // Prepare for drawing with pre-multiplication.
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        for (int i = 0; i < Arms.Count; i++)
            Arms[i].Draw(screenPos, NPC);

        Main.spriteBatch.End();
    }

    public void DrawArmsWithShader(Vector2 screenPos)
    {
        // Initialize the arm drawer, with the Avatar as its current host.
        ArmDrawContents.Host = this;
        ArmDrawContents.Request();

        // If the arm drawer is ready, draw it to the screen.
        if (!ArmDrawContents.IsReady)
            return;

        // Prepare for shader drawing.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, TransformPerspective);

        float[] blurWeights = new float[7];
        for (int i = 0; i < blurWeights.Length; i++)
            blurWeights[i] = GaussianDistribution(i - blurWeights.Length * 0.5f, 2.475f) / 7f;

        float blurOffset = 0.0006f;

        // Increase the intensity of the red outline by making the blur go out more during the flailing frenzy attack, to subtly suggest that they do contact damage.
        if (CurrentAttack == RiftAttackType.FlailingFrenzy)
            blurOffset *= 1.9f;

        // Prepare the arm shader.
        Texture2D target = ArmDrawContents.GetTarget();
        ManagedShader armShader = ShaderManager.GetShader("NoxusBoss.AvatarShadowArmShader");
        armShader.TrySetParameter("blurWeights", blurWeights);
        armShader.TrySetParameter("blurOffset", blurOffset);
        armShader.TrySetParameter("blurAtCenter", true);
        armShader.TrySetParameter("performPositionCutoff", false);
        armShader.Apply();

        // Draw the arm target with a special shader.
        Vector2 drawPosition = NPC.Center - screenPos;
        DrawData targetData = new DrawData(target, drawPosition, target.Frame(), NPC.GetAlpha(Color.Red), 0f, target.Size() * 0.5f, 1f, 0, 0f);
        targetData.Draw(Main.spriteBatch);

        // Return to default drawing.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, TransformPerspective);
    }

    public override void BossHeadRotation(ref float rotation)
    {
        rotation = Main.GlobalTimeWrappedHourly * -5f + NPC.whoAmI;
    }

    #endregion Drawing

    #region Hit Effects and Loot

    public override bool CanHitNPC(NPC target) => false;

    // Ensure that the Avatar's contact damage adheres to the special boss-specific cooldown slot, to prevent things like lava cheese.
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        // Ensure that the Avatar's contact damage adheres to the special boss-specific cooldown slot, to prevent things like lava cheese.
        cooldownSlot = ImmunityCooldownID.Bosses;

        // This is quite scuffed, but since there's no equivalent easy Colliding hook for NPCs, it is necessary to increase the Avatar's "effective hitbox" to an extreme
        // size via a detour and then use the CanHitPlayer hook to selectively choose whether the target should be inflicted damage or not (in this case, based on hands that can do damage).
        // This is because NPC collisions are fundamentally based on rectangle intersections. CanHitPlayer does not allow for the negation of that. But by increasing the hitbox by such an
        // extreme amount that that check is always passed, this issue is mitigated. Again, scuffed, but the onus is on TML to make this easier for modders to do.
        if (Arms.Where(h => h.CanDoDamage).Any())
            return Arms.Where(h => h.CanDoDamage).Any(h => Utils.CenteredRectangle(h.Center, Vector2.One * NPC.scale * 75f).Intersects(target.Hitbox));

        return CurrentAttack == RiftAttackType.FlailingFrenzy && NPC.ai[2] == 1f;
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (NPC.soundDelay >= 1)
            return;

        NPC.soundDelay = 9;
        SoundEngine.PlaySound(GennedAssets.Sounds.NPCHit.AvatarRiftHurt with { PitchVariance = 0.1f, Volume = 0.95f }, NPC.Center);
    }

    public override bool CheckDead()
    {
        AITimer = 0f;

        // Disallow natural death. The time check here is as a way of catching cases where multiple hits happen on the same frame and trigger a death.
        // If it just checked the attack state, then hit one would trigger the state change, set the HP to one, and the second hit would then deplete the
        // single HP and prematurely kill the Rift.
        if (CurrentAttack == RiftAttackType.DeathAnimation && AITimer >= 10f)
            return true;

        TriggerDeathAnimation();
        return false;
    }

    public override void BossLoot(ref string name, ref int potionType)
    {
        potionType = ItemID.SuperHealingPotion;
        if (ModReferences.Calamity?.TryFind("OmegaHealingPotion", out ModItem potion) ?? false)
            potionType = potion.Type;
    }

    #endregion Hit Effects and Loot

    #region Gotta Manually Disable Despawning Lmao

    // Disable natural despawning for the Rift.
    public override bool CheckActive() => false;

    #endregion Gotta Manually Disable Despawning Lmao
}
