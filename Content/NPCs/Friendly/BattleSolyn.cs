using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.NPCs.Bosses.Avatar;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.ParadiseReclaimed;
using NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.WorldSaving;

using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Friendly;

[AutoloadHead]
public partial class BattleSolyn : ModNPC
{
    public enum SolynAIType
    {
        FightMars,
        FightAvatar
    }

    #region Fields and Properties

    internal static readonly Dictionary<int, NPC> solynMappingsCacheByIndex = new Dictionary<int, NPC>(Main.maxNPCs);

    internal static InstancedRequestableTarget BaseSolynTarget;

    /// <summary>
    /// Solyn's immunity frame countdown value.
    /// </summary>
    public int ImmunityFrameCounter
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Solyn should be rendered as a fake ghost for her given battle due to being dead.
    /// </summary>
    public bool FakeGhostForm
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this Solyn instance is invisible or not.
    /// </summary>
    public bool Invisible
    {
        get;
        set;
    }

    /// <summary>
    /// Solyn's general-purpose AI timer, for use with her current state.
    /// </summary>
    public int AITimer
    {
        get => (int)NPC.ai[1];
        set => NPC.ai[1] = value;
    }

    /// <summary>
    /// The amount of afterimages Solyn should draw.
    /// </summary>
    public int AfterimageCount
    {
        get;
        set;
    } = 8;

    /// <summary>
    /// How much Solyn's afterimages clump together.
    /// </summary>
    public float AfterimageClumpInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// How much Solyn's afterimages glow.
    /// </summary>
    public float AfterimageGlowInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The scale factor of Solyn's backglow.
    /// </summary>
    public float BackglowScale
    {
        get;
        set;
    }

    /// <summary>
    /// The scale of Solyn's map icon in the world.
    /// </summary>
    public float WorldMapIconScale
    {
        get;
        set;
    }

    /// <summary>
    /// The intensity of the paradise reclaimed static effect over Solyn.
    /// </summary>
    public float StaticOverlayInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The intensity of the paradise reclaimed static dissolve effect over Solyn.
    /// </summary>
    public float StaticDissolveInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// An optional action that can be used to specify an effect that renders before all of Solyn's render actions.
    /// </summary>
    public Action<Vector2>? OptionalPreDrawRenderAction
    {
        get;
        set;
    }

    /// <summary>
    /// An optional action that can be used to specify an effect that renders after the rest of Solyn's render actions.
    /// </summary>
    public Action<Vector2>? OptionalPostDrawRenderAction
    {
        get;
        set;
    }

    /// <summary>
    /// Solyn's current AI state.
    /// </summary>
    public SolynAIType CurrentState
    {
        get => (SolynAIType)NPC.ai[0];
        set => NPC.ai[0] = (int)value;
    }

    /// <summary>
    /// The index of this Solyn instance in multiplayer.
    /// </summary>
    public int MultiplayerIndex
    {
        get => (int)NPC.ai[2];
        set => NPC.ai[2] = value;
    }

    /// <summary>
    /// Player related to this instance of Solyn
    /// </summary>
    public Player Player => Main.player[MultiplayerIndex];

    /// <summary>
    /// Whether the client associated with this Solyn instance is invalid in some way. This only applies to multiplayer clones.
    /// </summary>
    public bool AssociatedClientIsInvalid
    {
        get
        {
            if (!IsMultiplayerClone)
                return false;

            bool mappedToInvalidIndex = MultiplayerIndex <= -1 || MultiplayerIndex >= Main.maxPlayers;
            if (mappedToInvalidIndex)
            {
                NPC.active = false;
                return true;
            }

            Player player = Main.player[MultiplayerIndex];
            bool associatedClientIsGone = !player.active || player.dead;
            if (associatedClientIsGone)
            {
                NPC.active = false;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Whether this Solyn instance is a clone created for multiplayer purposes.
    /// </summary>
    public bool IsMultiplayerClone => NPC.ai[3] == 1;

    /// <summary>
    /// The currently frame Solyn should use on her sprite sheet.
    /// </summary>
    public ref float Frame => ref NPC.localAI[0];

    /// <summary>
    /// How many frames of immunity Solyn receives upon taking damage.
    /// </summary>
    public static int ImmunityFramesGrantedOnHit => SecondsToFrames(0.67f);

    public override string Texture => GetAssetPath("Content/NPCs/Friendly", Name);

    #endregion Fields and Properties

    #region Initialization
    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 26;

        // Ensure Solyn is registered as a pseudo-town-NPC.
        // This allows for certain behaviors such as mutual interaction between town NPCs.
        NPCID.Sets.ActsLikeTownNPC[Type] = true;
        NPCID.Sets.ShimmerTownTransform[Type] = true;
        NPCID.Sets.TrailingMode[Type] = 3;
        NPCID.Sets.TrailCacheLength[Type] = 45;

        EmptinessSprayer.NPCsToNotDelete[Type] = true;

        this.ExcludeFromBestiary();

        if (Main.netMode != NetmodeID.Server)
        {
            BaseSolynTarget = new InstancedRequestableTarget();
            Main.ContentThatNeedsRenderTargets.Add(BaseSolynTarget);
        }
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 0f;

        // Set up hitbox data.
        NPC.width = 40;
        NPC.height = 68;

        // Define stats.
        NPC.damage = 0;
        NPC.defense = 0;
        NPC.lifeMax = 12000;

        // Fly through all liquids quickly.
        NPC.lavaMovementSpeed = 1f;
        NPC.waterMovementSpeed = 1f;
        NPC.honeyMovementSpeed = 1f;

        // Do not use any default AI states.
        NPC.aiStyle = -1;
        AIType = -1;

        // Use 80% knockback resistance.
        NPC.knockBackResist = 0.2f;

        // Enable gravity and tile collision.
        NPC.noGravity = false;
        NPC.noTileCollide = false;

        // Be immune to lava.
        NPC.lavaImmune = true;

        // Disable damage from hostile NPCs.
        NPC.dontTakeDamageFromHostiles = true;

        // Set the hit sound.
        NPC.HitSound = SoundID.NPCHit1;

        // Act as a pseudo-town NPC.
        NPC.friendly = true;
    }

    #endregion Initialization

    #region AI
    public override void AI()
    {
        NPC.noGravity = false;
        NPC.immortal = false;
        NPC.dontTakeDamage = true;
        NPC.gfxOffY = 0f;
        NPC.townNPC = true;
        NPC.hide = true;
        NPC.ShowNameOnHover = NPC.Opacity >= 0.35f;
        Invisible = false;
        BackglowScale = Lerp(BackglowScale, 1f, 0.02f);
        WorldMapIconScale = Lerp(WorldMapIconScale, 1f, 0.03f);
        OptionalPreDrawRenderAction = null;
        OptionalPostDrawRenderAction = null;

        // Disallow an undefined sprite direction.
        // If it ends up being 0, it'll become 1 by default.
        // If it is not -1 or 1, it'll become the the sign of the original value.
        NPC.spriteDirection = (NPC.spriteDirection >= 0).ToDirectionInt();

        // Make Solyn fake if her real form is dead, the world was generated before the Avatar update, or if this is a clone created for multiplayer compatibility reasons.
        FakeGhostForm = BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() || WorldVersionSystem.PreAvatarUpdateWorld || IsMultiplayerClone;

        ExecuteCurrentBehavior();

        if (AssociatedClientIsInvalid)
        {
            NPC.active = false;
            return;
        }

        if (Invisible)
            NPC.ShowNameOnHover = false;

        // Emit a tiny bit of light.
        DelegateMethods.v3_1 = new Vector3(0.3f, 0.367f, 0.45f) * 0.8f;
        Utils.PlotTileLine(NPC.Top, NPC.Bottom, NPC.width, DelegateMethods.CastLightOpen);

        // This is necessary to ensure that the map icon is correct.
        NPC.direction = -NPC.spriteDirection;

        // NOOOO SOLYN DON'T DESPAWN!
        NPC.timeLeft = 7200;

        // Update timers.
        AITimer++;
    }

    public void UseStarFlyEffects()
    {
        // Release star particles.
        int starPoints = Main.rand.Next(3, 9);
        float starScaleInterpolant = Main.rand.NextFloat();
        int starLifetime = (int)Lerp(11f, 30f, starScaleInterpolant);
        float starScale = Lerp(0.2f, 0.4f, starScaleInterpolant) * NPC.scale;
        Color starColor = Color.Lerp(new(1f, 0.41f, 0.51f), new(1f, 0.85f, 0.37f), Main.rand.NextFloat());

        if (FakeGhostForm)
            starColor = Color.White;

        Vector2 starSpawnPosition = NPC.Center + new Vector2(NPC.spriteDirection * 10f, 8f) + Main.rand.NextVector2Circular(16f, 16f);
        Vector2 starVelocity = Main.rand.NextVector2Circular(3f, 3f) + NPC.velocity * (1f - GameSceneSlowdownSystem.SlowdownInterpolant);
        TwinkleParticle star = new TwinkleParticle(starSpawnPosition, starVelocity, starColor, starLifetime, starPoints, new Vector2(Main.rand.NextFloat(0.4f, 1.6f), 1f) * starScale, starColor * 0.5f);
        star.Spawn();

        Frame = 25f;
    }

    /// <summary>
    /// Executes Solyn's current behavior state.
    /// </summary>
    public void ExecuteCurrentBehavior()
    {
        switch (CurrentState)
        {
            case SolynAIType.FightMars:
                DoBehavior_FightMars();
                break;
            case SolynAIType.FightAvatar:
                DoBehavior_FightAvatar();
                break;
        }
    }

    /// <summary>
    /// Changed player related to Solyn and teleports her to him
    /// </summary>
    public void SwitchTo(Player player, bool playTeleportEffect = true)
    {
        //For some reason things are bad when client tries to execute this code, so better not to do it
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        if (!player.active || player.whoAmI == MultiplayerIndex || IsMultiplayerClone) return;

        Vector2 oldPosition = NPC.position;
        Vector2 newPosition;

        var targetSolyn = GetSolynRelatedTo(player);
        if (targetSolyn is null)
        {
            newPosition = player.position;

            MultiplayerIndex = player.whoAmI;
            NPC.netUpdate = true;
        }
        else
        {
            (MultiplayerIndex, targetSolyn.MultiplayerIndex) = (targetSolyn.MultiplayerIndex, MultiplayerIndex);

            newPosition = targetSolyn.NPC.position;
            targetSolyn.NPC.position = oldPosition;

            NPC.netUpdate = true;
            targetSolyn.NPC.netUpdate = true;
        }

        if (playTeleportEffect) 
        {
            PlayTeleportEffect(oldPosition, newPosition);
        }
    }

    public static void PlayTeleportEffect(Vector2 from, Vector2 to)
    {
        if (Main.netMode == NetmodeID.Server)
        {
            PacketManager.SendPacket<BattleSolynTeleportEffectPacket>(from, to);
            return;
        }

        // Create teleport particles at the starting position.
        ExpandingGreyscaleCircleParticle circle = new ExpandingGreyscaleCircleParticle(from, Vector2.Zero, Color.IndianRed, 8, 0.1f);
        circle.Spawn();
        MagicBurstParticle burst = new MagicBurstParticle(from, Vector2.Zero, Color.Wheat, 20, 1.04f);
        burst.Spawn();

        // Play a teleport out sound.
        SoundEngine.PlaySound(GennedAssets.Sounds.Common.TeleportOut with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 5, PitchVariance = 0.16f }, from);

        // Create teleport particles at the ending position.
        circle = new(to, Vector2.Zero, Color.IndianRed, 8, 0.1f);
        circle.Spawn();
        burst = new(to, Vector2.Zero, Color.Wheat, 20, 1.04f);
        burst.Spawn();

        // Play a teleport in sound.
        SoundEngine.PlaySound(GennedAssets.Sounds.Common.TeleportIn with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 5, PitchVariance = 0.16f }, from);
    }

    /// <summary>
    /// Attempts to summon Solyn for a given battle.
    /// </summary>
    /// <param name="spawnSource">The spawn source for Solyn.</param>
    /// <param name="spawnPosition">The position at which Solyn should be summoned.</param>
    /// <param name="state">The state Solyn should be summoned with.</param>
    public static void SummonSolynForBattle(IEntitySource spawnSource, Vector2 spawnPosition, SolynAIType state)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        bool combatSolynExists = false;
        int normalSolynID = ModContent.NPCType<Solyn>();
        int combatSolynID = ModContent.NPCType<BattleSolyn>();
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.type == normalSolynID)
            {
                npc.Transform(combatSolynID);
                npc.As<BattleSolyn>().CurrentState = state;
                npc.netUpdate = true;
                combatSolynExists = true;
            }

            if (npc.type == combatSolynID)
                combatSolynExists = true;
        }

        if (!combatSolynExists)
            NPC.NewNPC(spawnSource, (int)spawnPosition.X, (int)spawnPosition.Y, ModContent.NPCType<BattleSolyn>(), 1, (int)state);

        SummonSolynForBattle_CreateMPClones(spawnSource, spawnPosition, state);
    }

    private static void SummonSolynForBattle_CreateMPClones(IEntitySource spawnSource, Vector2 spawnPosition, SolynAIType state)
    {
        solynMappingsCacheByIndex.Clear();

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is not BattleSolyn solyn)
                continue;

            solynMappingsCacheByIndex[solyn.MultiplayerIndex] = npc;
        }

        foreach (Player player in Main.ActivePlayers)
        {
            if (player.dead)
                continue;

            if (!solynMappingsCacheByIndex.ContainsKey(player.whoAmI))
                NPC.NewNPC(spawnSource, (int)spawnPosition.X, (int)spawnPosition.Y, ModContent.NPCType<BattleSolyn>(), 1, (int)state, 0f, player.whoAmI, 1);
        }
    }

    public static BattleSolyn? GetOriginalSolyn()
    {
        foreach (var npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is BattleSolyn solyn && !solyn.IsMultiplayerClone) return solyn;
        }

        return null;
    }

    public static BattleSolyn? GetSolynRelatedTo(Player player)
    {
        foreach (var npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is BattleSolyn solyn && solyn.Player == player) return solyn;
        }

        return null;
    }

    #endregion AI

    #region Collision

    public override bool? CanBeHitByProjectile(Projectile projectile)
    {
        if (projectile.hostile && ImmunityFrameCounter <= 0)
            return true;
        return null;
    }

    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        ImmunityFrameCounter = ImmunityFramesGrantedOnHit;
    }

    #endregion Collision

    #region Drawing

    // This is a bit clunky but it's necessary for Solyn to be interactable as a town NPC.
    // Her actual UI is drawn separately.
    public override string GetChat() => string.Empty;

    public void PerformStandardFraming()
    {
        if (Abs(NPC.velocity.X) <= 0.1f)
        {
            int defaultFrame = 0;
            int blinkFrame = 20;
            Frame = AITimer % 150 >= 147 ? blinkFrame : defaultFrame;
        }
        else
        {
            NPC.frameCounter++;
            if (NPC.frameCounter >= 5)
            {
                Frame++;
                NPC.frameCounter = 0;
            }

            int minFrame = 3;
            int maxFrame = 15;
            bool running = Abs(NPC.velocity.X) >= 9f;
            if (running)
            {
                minFrame = 26;
                maxFrame = 39;
            }

            if (Frame < minFrame)
            {
                // Ensure that running frames are seamlessly moved into instead of resetting the entire animation.
                if (running)
                    Frame += minFrame;
                else
                    Frame = minFrame;
            }
            if (Frame >= maxFrame)
                Frame = minFrame;
        }
    }

    public override void FindFrame(int frameHeight)
    {
        if (NPC.IsABestiaryIconDummy)
        {
            NPC.velocity.X = 5f;
            PerformStandardFraming();
        }

        // Set Solyn's frame.
        NPC.frame.Width = 62;
        NPC.frame.X = (int)(Frame / Main.npcFrameCount[Type]) * NPC.frame.Width;
        NPC.frame.Y = (int)(Frame % Main.npcFrameCount[Type]) * frameHeight;
    }

    // Have a perpetual slight bright glow at all times.
    public override Color? GetAlpha(Color drawColor)
    {
        if (FakeGhostForm)
            return drawColor * NPC.Opacity;

        float immunityPulse = 1f - Cos01(TwoPi * ImmunityFrameCounter / ImmunityFramesGrantedOnHit * 2f);
        Color baseColor = Color.Lerp(drawColor, Color.White, 0.2f);
        Color immunityColor = Color.Lerp(drawColor, new(255, 0, 50), 0.9f);
        Color color = Color.Lerp(baseColor, immunityColor, immunityPulse) * Lerp(1f, 0.3f, immunityPulse);
        return color * NPC.Opacity * (1f - NPC.shimmerTransparency);
    }

    public override void ModifyTypeName(ref string typeName)
    {
        if (Main.gameMenu)
            return;

        // Choose Solyn's name.
        NPC.GivenName = string.Empty;
    }

    public override void BossHeadSpriteEffects(ref SpriteEffects spriteEffects)
    {
        spriteEffects = NPC.spriteDirection.ToSpriteDirection();
    }

    public override void DrawBehind(int index)
    {
        Main.instance.DrawCacheNPCsOverPlayers.Add(index);
    }

    public void DrawBackglow(Vector2 drawPosition)
    {
        Matrix matrix = NPC.IsABestiaryIconDummy ? Main.UIScaleMatrix : Main.GameViewMatrix.TransformationMatrix;
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, DefaultRasterizerScreenCull, null, matrix);

        if (Frame >= 22)
            drawPosition.Y += 20f;

        float backglowOpacityFactor = (BackglowScale - 1f) * 0.7f + 1f;
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, NPC.GetAlpha(new(0.8f, 1f, 0.7f)) * (backglowOpacityFactor * 0.4f), 0f, BloomCircleSmall.Size() * 0.5f, NPC.scale * BackglowScale * 0.38f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, drawPosition, null, NPC.GetAlpha(new(0.05f, 0.3f, 1f)) * (backglowOpacityFactor * 0.25f), 0f, BloomCircleSmall.Size() * 0.5f, NPC.scale * BackglowScale * 0.7f, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, DefaultRasterizerScreenCull, null, matrix);
    }

    private void PrepareBaseTarget(Color lightColor)
    {
        int identifier = NPC.IsABestiaryIconDummy ? -1 : NPC.whoAmI;
        Vector2 targetSize = new Vector2(384f);
        BaseSolynTarget.Request((int)targetSize.X, (int)targetSize.Y, identifier, () =>
        {
            Main.spriteBatch.Begin(FakeGhostForm ? SpriteSortMode.Immediate : SpriteSortMode.Deferred, BlendState.AlphaBlend);

            Vector2 drawPosition = targetSize * 0.5f;

            // Draw Solyn.
            Color afterimageColor = new Color(0f, 0.25f, 1f, 0f);
            Color glowmaskColor = Color.White;
            Rectangle frame = NPC.frame;
            Texture2D texture = TextureAssets.Npc[Type].Value;
            SpriteEffects direction = NPC.spriteDirection.ToSpriteDirection() ^ SpriteEffects.FlipHorizontally;

            if (FakeGhostForm)
            {
                lightColor = Color.White * 0.6f;
                glowmaskColor = Color.Transparent;
                afterimageColor = lightColor;
                afterimageColor.A = 0;

                ManagedShader soulShader = ShaderManager.GetShader("NoxusBoss.FakeSolynShader");
                soulShader.TrySetParameter("imageSize", texture.Size());
                soulShader.TrySetParameter("sourceRectangle", new Vector4(NPC.frame.X, NPC.frame.Y, NPC.frame.Width, NPC.frame.Height));
                soulShader.Apply();
            }

            if (StaticOverlayInterpolant <= 0f && AvatarOfEmptinessSky.Dimension != AvatarDimensionVariants.AntishadowDimension)
            {
                for (int i = AfterimageCount; i >= 0; i--)
                {
                    float afterimageInterpolant = i / (float)AfterimageCount;
                    Vector2 afterimageDrawPosition = drawPosition + NPC.oldPos[i] - NPC.position;
                    afterimageDrawPosition = Vector2.Lerp(afterimageDrawPosition, drawPosition, AfterimageClumpInterpolant);
                    float afterimageOpacity = Exp(afterimageInterpolant * -3.4f);

                    Main.EntitySpriteDraw(texture, afterimageDrawPosition, frame, NPC.GetAlpha(afterimageColor) * afterimageOpacity * AfterimageGlowInterpolant, NPC.rotation, frame.Size() * 0.5f, NPC.scale, direction);
                }
            }

            Texture2D glowmask = GennedAssets.Textures.Friendly.SolynGlow.Value;
            Main.EntitySpriteDraw(texture, drawPosition, frame, NPC.GetAlpha(lightColor), NPC.rotation, frame.Size() * 0.5f, NPC.scale, direction);
            Main.EntitySpriteDraw(glowmask, drawPosition, frame, NPC.GetAlpha(glowmaskColor) * 0.26f, NPC.rotation, frame.Size() * 0.5f, NPC.scale, direction);
            Main.spriteBatch.End();
        });
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (Invisible)
            return false;

        if (Main.instance.currentNPCShowingChatBubble == NPC.whoAmI)
            Main.instance.currentNPCShowingChatBubble = -1;

        Vector2 drawPosition = NPC.Center - screenPos + Vector2.UnitY * (NPC.gfxOffY - 6f);
        OptionalPreDrawRenderAction?.Invoke(drawPosition);
        PrepareBaseTarget(drawColor);

        int identifier = NPC.IsABestiaryIconDummy ? -1 : NPC.whoAmI;
        if (!BaseSolynTarget.TryGetTarget(identifier, out RenderTarget2D? target) || target is null)
            return false;

        // Draw a mild backglow.
        if (StaticOverlayInterpolant <= 0f && AvatarOfEmptinessSky.Dimension != AvatarDimensionVariants.AntishadowDimension)
            DrawBackglow(drawPosition);

        bool useStaticShader = StaticOverlayInterpolant > 0f || StaticDissolveInterpolant > 0f;
        if (useStaticShader)
        {
            Main.spriteBatch.PrepareForShaders();
            ManagedShader staticShader = ShaderManager.GetShader("NoxusBoss.SolynStaticOverlayShader");
            staticShader.TrySetParameter("dissolveInterpolant", StaticDissolveInterpolant);
            staticShader.TrySetParameter("imageSize", target.Size());
            staticShader.TrySetParameter("sourceRectangle", new Vector4(NPC.frame.X, NPC.frame.Y, NPC.frame.Width, NPC.frame.Height));
            staticShader.SetTexture(ParadiseStaticTargetSystem.StaticTarget, 1, SamplerState.PointWrap);
            staticShader.SetTexture(WavyBlotchNoise, 2, SamplerState.PointWrap);
            staticShader.Apply();
        }

        Main.spriteBatch.Draw(target, drawPosition, null, Color.White, 0f, target.Size() * 0.5f, NPC.scale, 0, 0f);

        if (useStaticShader)
            Main.spriteBatch.ResetToDefault();

        OptionalPostDrawRenderAction?.Invoke(drawPosition);

        return false;
    }

    #endregion Drawing

    #region I love automatic despawning

    public override bool CheckActive() => false;

    #endregion I love automatic despawning
}
