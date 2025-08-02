using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Content.Emotes;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.NPCs.Bosses.Avatar;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.Particles;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Graphics.TentInterior;
using NoxusBoss.Core.Graphics.UI.SolynDialogue;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.GameScenes.AvatarUniverseExploration;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using NoxusBoss.Core.World.Subworlds;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving;

using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

using static NoxusBoss.Core.CrossCompatibility.Inbound.CalamityRemix.CalRemixCompatibilitySystem;

namespace NoxusBoss.Content.NPCs.Friendly;

[AutoloadHead]
public partial class Solyn : ModNPC, IPixelatedPrimitiveRenderer
{
    /// <summary>
    /// Solyn's current frame on her overall sprite sheet.
    /// </summary>
    public int Frame;

    /// <summary>
    /// A countdown value that determines whether Solyn should use a shocked expression.
    /// </summary>
    public int ShockedExpressionCountdown
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of afterimages Solyn should have.
    /// </summary>
    public int AfterimageCount
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Solyn has a bright glow around herself or not.
    /// </summary>
    public bool HasBackglow
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Solyn is actually Soulyn.
    /// </summary>
    public bool SoulForm
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of squish Solyn has currently.
    /// </summary>
    public float Squish
    {
        get;
        set;
    }

    /// <summary>
    /// The zoom-in interpolant Solyn should use for the current client due to talking with them.
    /// </summary>
    public float ZoomInInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Solyn should reel in the kite she has or not.
    /// </summary>
    public bool ReelInKite
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Solyn should fall through platforms or not.
    /// </summary>
    public bool DescendThroughSlopes
    {
        get;
        set;
    }

    /// <summary>
    /// Solyn's current state.
    /// </summary>
    public SolynAIType CurrentState
    {
        get;
        set;
    }

    /// <summary>
    /// A general-purpose AI timer for Solyn.
    /// </summary>
    public int AITimer
    {
        get;
        set;
    }

    /// <summary>
    /// How long it's been since anyone last talked to Solyn.
    /// </summary>
    public int TimeSinceLastTalk
    {
        get;
        set;
    }

    /// <summary>
    /// The player Solyn is talking to.
    /// </summary>
    public int TalkingTo
    {
        get;
        set;
    }

    /// <summary>
    /// Solyn's effective scale, taking into account her <see cref="Squish"/>.
    /// </summary>
    public Vector2 EffectiveScale => new Vector2(1f + Squish, 1f - Squish) * NPC.scale;

    #region Initialization

    public override string Texture => GetAssetPath("Content/NPCs/Friendly", Name);

    public override void Load() => Mod.AddNPCHeadTexture(Type, $"{Texture}_Shimmer_Head");

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 26;

        // Ensure Solyn is registered as a pseudo-town-NPC.
        // This allows for certain behaviors such as mutual interaction between town NPCs.
        NPCID.Sets.ActsLikeTownNPC[Type] = true;
        NPCID.Sets.ShimmerTownTransform[Type] = true;
        NPCID.Sets.FaceEmote[Type] = ModContent.EmoteBubbleType<SolynEmote>();

        NPCID.Sets.TrailingMode[Type] = 3;
        NPCID.Sets.TrailCacheLength[Type] = 45;

        EmptinessSprayer.NPCsToNotDelete[Type] = true;

        FannyDialog itsPeak = new FannyDialog("FannyFuckingHATESSolyn", "FannyCryptid").WithDuration(22f).WithCondition(_ =>
        {
            int solynIndex = NPC.FindFirstNPC(ModContent.NPCType<Solyn>());
            if (solynIndex != -1 && CommonCalamityVariables.DraedonDefeated && CommonCalamityVariables.CalamitasDefeated && Main.rand.NextBool(3600))
                return Main.LocalPlayer.WithinRange(Main.npc[solynIndex].Center, 450f);

            return false;
        }).WithoutClickability().WithDrawSizes(1420);
        itsPeak.Register();

        On_Main.DrawNPCHeadFriendly += FlipMapHead; 
    }

    private void FlipMapHead(On_Main.orig_DrawNPCHeadFriendly orig, Entity entity, byte alpha, float headScale, SpriteEffects dir, int townHeadId, float x, float y)
    {
        if (entity is NPC npc && npc.type == ModContent.NPCType<Solyn>())
            dir ^= SpriteEffects.FlipHorizontally;

        orig(entity, alpha, headScale, dir, townHeadId, x, y);
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 32f;

        // Set up hitbox data.
        NPC.width = 28;
        NPC.height = 48;

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

        // Use 60% knockback resistance.
        NPC.knockBackResist = 0.4f;

        // Enable gravity and tile collision.
        NPC.noGravity = false;
        NPC.noTileCollide = false;

        // Be immune to lava.
        NPC.lavaImmune = true;

        // Disable damage from hostile NPCs.
        NPC.dontTakeDamageFromHostiles = true;

        // Set the hit sound.
        NPC.HitSound = SoundID.NPCHit1;

        // Act as a town NPC.
        NPC.friendly = true;
        NPC.townNPC = true;

        TownNPCStayingHomeless = true;
    }

    public override void OnSpawn(IEntitySource source)
    {

    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
        {
            BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
            new FlavorTextBestiaryInfoElement($"Mods.{Mod.Name}.Bestiary.{Name}")
        });
    }

    #endregion Initialization

    #region Network Code

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((int)CurrentState);
        writer.Write(TimeSinceLastTalk);
        writer.Write(AITimer);
        writer.Write(WanderAbout_StuckTimer);
        writer.Write(SkyFallDirection);
        writer.WriteVector2(WanderDestination);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        CurrentState = (SolynAIType)reader.ReadInt32();
        TimeSinceLastTalk = reader.ReadInt32();
        AITimer = reader.ReadInt32();
        WanderAbout_StuckTimer = reader.ReadInt32();
        SkyFallDirection = reader.ReadInt32();
        WanderDestination = reader.ReadVector2();
    }

    #endregion Network Code

    #region AI
    public override void AI()
    {
        // YOU'RE NOT SUPPOSED TO BE HERE!!
        if (BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() && !EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
        {
            NPC.active = false;
            return;
        }

        // Reset things every frame.
        NPC.immortal = true;
        NPC.gfxOffY = 0f;
        NPC.townNPC = true;
        NPC.hide = false;
        NPC.breath = 200;
        NPC.breathCounter = 0;
        NPC.Opacity = Saturate(NPC.Opacity + 0.01f);
        CanBeSpokenTo = TalkingTo == -1 || TalkingTo == Main.myPlayer;
        HasBackglow = false;
        SoulForm = false;
        Squish *= 0.85f;
        AfterimageCount = 8;
        if (EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
        {
            SoulForm = true;
            HasBackglow = true;
        }

        if (CurrentState != SolynAIType.PuppeteeredByQuest)
        {
            NPC.noGravity = false;
            DescendThroughSlopes = false;
        }

        // Disallow an undefined sprite direction.
        // If it ends up being 0, it'll become 1 by default.
        // If it is not -1 or 1, it'll become the the sign of the original value.
        NPC.spriteDirection = (NPC.spriteDirection >= 0).ToDirectionInt();

        RiftEclipseSnow.CreateSnowWalkEffects(NPC, false);

        // Stay at home if the player is in the ceaseless void rift.
        if (AvatarUniverseExplorationSystem.InAvatarUniverse)
        {
            NPC.Center = SolynCampsiteWorldGen.CampSitePosition - Vector2.UnitY * 32f;
            NPC.velocity = Vector2.Zero;
        }

        // Emit a tiny bit of light.
        DelegateMethods.v3_1 = new Vector3(0.3f, 0.367f, 0.45f) * 0.8f;
        Utils.PlotTileLine(NPC.Top, NPC.Bottom, NPC.width, DelegateMethods.CastLightOpen);

        // This is necessary to ensure that the map icon is correct.
        NPC.direction = -NPC.spriteDirection;

        // NOOOO SOLYN DON'T DESPAWN!
        NPC.timeLeft = 7200;

        switch (CurrentState)
        {
            case SolynAIType.StandStill:
                DoBehavior_StandStill();
                break;
            case SolynAIType.WanderAbout:
                DoBehavior_WanderAbout();
                break;
            case SolynAIType.SpeakToPlayer:
                DoBehavior_SpeakToPlayer();
                break;
            case SolynAIType.FallFromTheSky:
                DoBehavior_FallFromTheSky();
                break;
            case SolynAIType.GetUpAfterStarFall:
                DoBehavior_GetUpAfterStarFall();
                break;
            case SolynAIType.EnterTentToSleep:
                DoBehavior_EnterTentToSleep();
                break;
            case SolynAIType.Eepy:
                DoBehavior_Eepy();
                break;
            case SolynAIType.WaitToTeleportHome:
                if (AITimer >= 60)
                {
                    TeleportTo(SolynCampsiteWorldGen.CampSitePosition);
                    SwitchState(SolynAIType.StandStill);
                }
                break;
        }

        if (TimeSinceLastTalk >= 1)
            TimeSinceLastTalk++;

        HandleConversationEffects();

        // Zoom in on Solyn based on the zoom interpolant.
        if (ZoomInInterpolant > 0f && TalkingTo == Main.myPlayer)
        {
            CameraPanSystem.Zoom = Pow(ZoomInInterpolant, 0.7f) * 0.6f;
            CameraPanSystem.PanTowards(NPC.Center, ZoomInInterpolant);
            CalamityCompatibility.ResetStealthBarOpacity(Main.LocalPlayer);
        }

        // Increment the AI timer.
        AITimer++;
    }

    public void SwitchState(SolynAIType state)
    {
        CurrentState = state;
        AITimer = 0;
        NPC.netUpdate = true;
    }

    public void UseStarFlyEffects()
    {
        // Release star particles.
        int starPoints = Main.rand.Next(3, 9);
        float starScaleInterpolant = Main.rand.NextFloat();
        int starLifetime = (int)Lerp(11f, 30f, starScaleInterpolant);
        float starScale = Lerp(0.2f, 0.4f, starScaleInterpolant) * NPC.scale;
        Color starColor = Color.Lerp(new Color(1f, 0.41f, 0.51f), new Color(1f, 0.85f, 0.37f), Main.rand.NextFloat());
        Vector2 starSpawnPosition = NPC.Center + new Vector2(NPC.spriteDirection * 10f, 8f) + Main.rand.NextVector2Circular(16f, 16f);
        Vector2 starVelocity = Main.rand.NextVector2Circular(3f, 3f) + NPC.velocity;
        TwinkleParticle star = new TwinkleParticle(starSpawnPosition, starVelocity, starColor, starLifetime, starPoints, new Vector2(Main.rand.NextFloat(0.4f, 1.6f), 1f) * starScale, starColor * 0.5f);
        star.Spawn();

        Frame = 25;
    }

    public void SpeakToPlayerEffects()
    {
        TimeSinceLastTalk = 1;

        string currentDialogueUsedByUI = ModContent.GetInstance<SolynDialogSystem>().DialogUI.CurrentDialogueNode?.TextKey ?? string.Empty;
        if (!CurrentConversation.Tree.PossibleDialogue.Values.Any(d => d.TextKey == currentDialogueUsedByUI))
            ModContent.GetInstance<SolynDialogSystem>().DialogUI.SetDialogue(CurrentConversation.RootSelectionFunction());

        SolynDialogSystem.ShowUI();

        // Zoom in on Solyn.
        ZoomInInterpolant = Saturate(ZoomInInterpolant + 0.02f);

        if (DialogueManager.FindByRelativePrefix("SolynIntroduction").SeenBefore("Question1") || CurrentConversation != DialogueManager.FindByRelativePrefix("SolynIntroduction"))
            NPC.spriteDirection = (Main.LocalPlayer.Center.X - NPC.Center.X).NonZeroSign();

        // Switch to the speak-to-player AI state.
        if (CurrentState != SolynAIType.SpeakToPlayer && CurrentState != SolynAIType.PuppeteeredByQuest)
        {
            while (Collision.SolidCollision(NPC.BottomLeft - Vector2.UnitY * 2f, NPC.width, 2))
                NPC.position.Y -= 2f;

            AITimer = 0;
            CurrentState = SolynAIType.SpeakToPlayer;
            NPC.netUpdate = true;
        }
    }

    public void HandleConversationEffects()
    {
        if (Main.netMode == NetmodeID.Server) return;

        Player player = Main.LocalPlayer;
        if (TalkingTo == -1 && player.talkNPC == NPC.whoAmI)
        {
            TalkingTo = player.whoAmI;
            PacketManager.SendPacket<PlayerTalkToSolynPacket>(NPC.whoAmI, TalkingTo);
        }
        else if (TalkingTo == player.whoAmI && (player.talkNPC != NPC.whoAmI || !CanBeSpokenTo))
        {
            TalkingTo = -1;
            PacketManager.SendPacket<PlayerTalkToSolynPacket>(NPC.whoAmI, TalkingTo);
        }

        CurrentConversation ??= ConversationSelector.ChooseRandomSolynConversation(this);
        ConversationSelector.Evaluate(this);

        // Toggle the UI as necessary.
        if ((TalkingTo == player.whoAmI && CanBeSpokenTo) || ForcedConversation)
        {
            SpeakToPlayerEffects();
            return;
        }
        
        if (ForcedConversation)
            return;

        if (TalkingTo != player.whoAmI)
            SolynDialogSystem.HideUI();

        // Zoom out.
        ZoomInInterpolant = Saturate(ZoomInInterpolant - 0.06f);
    }

    #endregion AI

    #region Saving

    public override void SaveData(TagCompound tag)
    {
        tag["State"] = (int)CurrentState;
    }

    public override void LoadData(TagCompound tag)
    {
        CurrentState = (SolynAIType)tag.GetInt("State");
    }

    #endregion Saving

    #region Drawing

    public override bool CanChat() => CanBeSpokenTo;

    // This is a bit clunky but it's necessary for Solyn to be interactable as a town NPC.
    // Her actual UI is drawn separately.
    public override string GetChat() => string.Empty;

    /// <summary>
    /// Makes Solyn teleport to a given position.
    /// </summary>
    /// <param name="teleportGroundPosition">Where Solyn's <see cref="Entity.Bottom"/> position should be teleported to.</param>
    public void TeleportTo(Vector2 teleportGroundPosition)
    {
        // Create teleport particles at the starting position.
        ExpandingGreyscaleCircleParticle circle = new ExpandingGreyscaleCircleParticle(NPC.Center, Vector2.Zero, Color.IndianRed, 8, 0.1f);
        circle.Spawn();
        MagicBurstParticle burst = new MagicBurstParticle(NPC.Center, Vector2.Zero, Color.Wheat, 20, 1.04f);
        burst.Spawn();

        // Play a teleport sound.
        SoundEngine.PlaySound(GennedAssets.Sounds.Common.TeleportOut with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 5, PitchVariance = 0.16f }, NPC.Center);

        // Teleport.
        NPC.Bottom = teleportGroundPosition;

        // Create teleport particles at the ending position.
        circle = new(NPC.Center, Vector2.Zero, Color.IndianRed, 8, 0.1f);
        circle.Spawn();
        burst = new(NPC.Center, Vector2.Zero, Color.Wheat, 20, 1.04f);
        burst.Spawn();
    }

    public void PerformStandardFraming()
    {
        if (Abs(NPC.velocity.X) <= 0.1f)
        {
            int defaultFrame = 0;
            int blinkFrame = 20;
            if (ShockedExpressionCountdown >= 1)
            {
                ShockedExpressionCountdown--;
                defaultFrame = 42;
            }

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
        NPC.frame.X = Frame / Main.npcFrameCount[Type] * NPC.frame.Width;
        NPC.frame.Y = Frame % Main.npcFrameCount[Type] * frameHeight;
    }

    public override bool? CanFallThroughPlatforms()
    {
        if (DescendThroughSlopes)
            return true;

        return null;
    }

    public override void ModifyTypeName(ref string typeName)
    {
        if (Main.gameMenu)
            return;

        NPC.GivenName = string.Empty;
        if (!DialogueManager.FindByRelativePrefix("SolynIntroduction").SeenBefore("Talk4") && !EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
            typeName = "???";
    }

    public override void BossHeadSpriteEffects(ref SpriteEffects spriteEffects)
    {
        spriteEffects = NPC.spriteDirection.ToSpriteDirection();
    }

    public override void DrawBehind(int index)
    {
        if (ModContent.GetInstance<EndCreditsScene>().IsActive && NamelessDeityBoss.Myself is not null)
        {
            Main.instance.DrawCacheNPCsBehindNonSolidTiles.Add(index);
            return;
        }

        if (CurrentState == SolynAIType.FallFromTheSky)
        {
            Main.instance.DrawCacheNPCsOverPlayers.Add(index);
            return;
        }

        if (SolynTentInteriorRenderer.CloseToTentTimer >= 1)
            SpecialLayeringSystem.DrawCacheOverTent.Add(index);
        else
            Main.instance.DrawCacheNPCProjectiles.Add(index);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (Main.instance.currentNPCShowingChatBubble == NPC.whoAmI && !CanBeSpokenTo)
            Main.instance.currentNPCShowingChatBubble = -1;

        Vector2 drawPosition = NPC.Center - screenPos + Vector2.UnitY * (NPC.gfxOffY - 6f);
        if (NPC.IsShimmerVariant)
        {
            Texture2D shimmerTexture = ModContent.Request<Texture2D>($"{Texture}_Shimmer").Value;
            Main.EntitySpriteDraw(shimmerTexture, drawPosition, null, NPC.GetAlpha(drawColor), NPC.rotation, shimmerTexture.Size() * 0.5f, EffectiveScale, 0);
            return false;
        }

        // Draw Solyn.
        Color glowmaskColor = Color.White;
        Rectangle frame = NPC.frame;
        Texture2D texture = TextureAssets.Npc[Type].Value;

        if (SoulForm)
        {
            Main.spriteBatch.PrepareForShaders();

            glowmaskColor = new(255, 178, 97);
            drawColor = glowmaskColor;

            ManagedShader soulShader = ShaderManager.GetShader("NoxusBoss.SoulynShader");
            soulShader.TrySetParameter("outlineOnly", true);
            soulShader.TrySetParameter("imageSize", texture.Size());
            soulShader.TrySetParameter("sourceRectangle", new Vector4(NPC.frame.X, NPC.frame.Y, NPC.frame.Width, NPC.frame.Height));
            soulShader.Apply();
        }

        SpriteEffects direction = NPC.spriteDirection.ToSpriteDirection() ^ SpriteEffects.FlipHorizontally;
        if (HasBackglow)
        {
            Color backglowColor = (SoulForm ? Color.White : Color.Cyan) with { A = 0 };
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                    Main.EntitySpriteDraw(texture, drawPosition + (TwoPi * i / 4f).ToRotationVector2() * 2f, frame, NPC.GetAlpha(backglowColor), NPC.rotation, frame.Size() * 0.5f, EffectiveScale, direction);
            }
        }

        if (SoulForm)
        {
            ManagedShader soulShader = ShaderManager.GetShader("NoxusBoss.SoulynShader");
            soulShader.TrySetParameter("outlineOnly", false);
            soulShader.Apply();
        }

        if (AvatarOfEmptinessSky.Dimension != AvatarDimensionVariants.AntishadowDimension)
        {
            for (int i = AfterimageCount; i >= 0; i--)
            {
                Vector2 afterimageDrawPosition = drawPosition + NPC.oldPos[i] - NPC.position;

                Color afterimageColor = new Color(0f, 0.25f, 1f, 0f);
                Main.EntitySpriteDraw(texture, afterimageDrawPosition, frame, NPC.GetAlpha(afterimageColor) * (1f - i / (float)AfterimageCount) * 0.2f, NPC.rotation, frame.Size() * 0.5f, EffectiveScale, direction);
            }
        }

        Main.EntitySpriteDraw(texture, drawPosition, frame, NPC.GetAlpha(drawColor), NPC.rotation, frame.Size() * 0.5f, EffectiveScale, direction);
        Main.EntitySpriteDraw(GennedAssets.Textures.Friendly.SolynGlow.Value, drawPosition, frame, NPC.GetAlpha(glowmaskColor) * 0.26f, NPC.rotation, frame.Size() * 0.5f, EffectiveScale, direction);

        if (SoulForm)
            Main.spriteBatch.ResetToDefault();

        return false;
    }

    public float StarFallTrailWidthFunction(float completionRatio) => NPC.scale * Utils.Remap(completionRatio, 0f, 0.9f, 32f, 1f);

    public Color StarFallTrailColorFunction(float completionRatio) => NPC.GetAlpha(new(75, 128, 250)) * Sqrt(1f - completionRatio);

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        if (CurrentState != SolynAIType.FallFromTheSky)
            return;

        PrimitiveSettings settings = new PrimitiveSettings(StarFallTrailWidthFunction, StarFallTrailColorFunction, _ => NPC.Size * 0.5f + NPC.velocity.SafeNormalize(Vector2.Zero) * 4f, Pixelate: true);
        PrimitiveRenderer.RenderTrail(NPC.oldPos.Take(5), settings, 42);
    }

    #endregion Drawing

    #region I love automatic despawning

    public override bool CheckActive() => false;

    #endregion I love automatic despawning
}
