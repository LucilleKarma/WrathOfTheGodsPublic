using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.Utilities;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Enemies.RiftEclipse;

public class Mirrorwalker : ModNPC
{
    #region Initialization

    public Player Target => Main.player[NPC.target];

    /// <summary>
    /// The X position of the mirror upon which this mirrorwalker reflects relative to the player position.
    /// </summary>
    public ref float MirrorX => ref NPC.ai[0];

    /// <summary>
    /// How much this mirrorwalker should transform into its dark form.
    /// </summary>
    public ref float HorrorInterpolant => ref NPC.ai[2];

    /// <summary>
    /// The render target that contains all render data for the bestiary entry.
    /// </summary>
    public static InstancedRequestableTarget BestiaryTarget
    {
        get;
        set;
    } = new InstancedRequestableTarget();

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        NPCID.Sets.MustAlwaysDraw[Type] = true;
        Main.ContentThatNeedsRenderTargets.Add(BestiaryTarget);
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 30f;
        NPC.damage = 0;
        NPC.width = 26;
        NPC.height = 48;
        NPC.defense = 8;
        NPC.lifeMax = 1700;
        NPC.aiStyle = -1;
        AIType = -1;
        NPC.knockBackResist = 0f;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.Opacity = 0f;
        NPC.ShowNameOnHover = false;
        NPC.dontTakeDamage = true;
        NPC.hide = true;
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        int avatarID = ModContent.NPCType<AvatarOfEmptiness>();
        bestiaryEntry.UIInfoProvider = new HighestOfMultipleUICollectionInfoProvider(new CommonEnemyUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[Type], true),
                                                                                     new CommonEnemyUICollectionInfoProvider(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[avatarID], true));
        bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
        {
            BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
            new FlavorTextBestiaryInfoElement($"Mods.{Mod.Name}.Bestiary.{Name}")
        });
    }

    #endregion Initialization

    #region AI
    public override void AI()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            // Ensure that the player's shader drawer is in use so that the render target contents can be used by this NPC.
            LocalPlayerDrawManager.StopCondition = () => !NPC.active;
            LocalPlayerDrawManager.ShaderDrawAction = () => { };
        }

        // Disable natural despawning.
        NPC.timeLeft = 7200;

        // Define the mirror position at first.
        // This is where the clone will be drawn relative to.
        if (MirrorX == 0f)
        {
            NPC.TargetClosest();
            MirrorX = Target.Center.X + Target.direction * Main.rand.NextFloat(960f, 1100f);
            NPC.Center = new Vector2(MirrorX + Target.direction * 10f, Target.Center.Y);
            NPC.netUpdate = true;
        }

        // Define position and opacity relative to the mirror.
        float distanceFromMirror = Distance(MirrorX, Target.Center.X);
        float mirrorSign = (NPC.Center.X - MirrorX).NonZeroSign();
        HorrorInterpolant = InverseLerp(50f, 16f, distanceFromMirror);
        NPC.Opacity = InverseLerp(485f, 200f, distanceFromMirror);
        NPC.Center = new Vector2(MirrorX + mirrorSign * distanceFromMirror, Target.Center.Y);
        NPC.gfxOffY = 0f;

        bool inTiles = Collision.SolidCollision(NPC.Center, 4, Target.height / 2);
        if (inTiles)
        {
            while (Collision.SolidCollision(NPC.Center, 4, Target.height / 2))
                NPC.position.Y -= 2f;
        }
        else
        {
            float oldY = NPC.position.Y;
            while (!Collision.SolidCollision(NPC.Center + Vector2.UnitY * (Target.height / 2 - 2f), 4, 2))
            {
                NPC.position.Y += 2f;
                if (NPC.position.Y >= Main.maxTilesY * 16f)
                {
                    NPC.active = false;
                    return;
                }
            }

            if (Distance(NPC.position.Y, oldY) >= 24f)
                NPC.position.Y = oldY;
            else
            {
                float _ = 1f;
                Collision.StepDown(ref NPC.position, ref NPC.velocity, Target.width, Target.head, ref _, ref NPC.gfxOffY);
            }
        }

        // Check if the Mirrorwalker is incredibly close to the player it's copying.
        // If it is, disappear and disorient the player.
        if (distanceFromMirror <= 6f)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.Glitch with { PitchVariance = 0.2f, Volume = 1.3f, MaxInstances = 8 });
            SoundEngine.PlaySound(GennedAssets.Sounds.RiftEclipse.RiftEclipseFogAmbience with { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, Volume = 0.7f });
            SoundMufflingSystem.MuffleFactor = 0.1f;
            MusicVolumeManipulationSystem.MuffleFactor = 0.1f;

            TotalScreenOverlaySystem.OverlayInterpolant = 2.3f;
            TotalScreenOverlaySystem.OverlayColor = Color.Black;

            // Manipulate time events in single player as an implication that the player went unconscious.
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                int unconsciousTime = Main.rand.Next(7200, 27000);
                RiftEclipseFogEventManager.IncrementTime(unconsciousTime / 5);

                // Stop all target movement.
                Target.velocity.X = 0f;

                int dayCycleLength = (int)(Main.dayTime ? Main.dayLength : Main.nightLength);
                int maxTimeStep = (int)Clamp(unconsciousTime, 0f, dayCycleLength - (int)Main.time);
                Main.time += unconsciousTime;
                if (Main.time > dayCycleLength - 1f)
                    Main.time = dayCycleLength - 1f;
            }

            Main.BestiaryTracker.Kills.RegisterKill(NPC);
            ScreenShakeSystem.StartShake(12f);
            GeneralScreenEffectSystem.ChromaticAberration.Start(NPC.Center, 4f, 240);
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.EarRinging with { Volume = 0.56f, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, MaxInstances = 1 });
            NPC.active = false;
            return;
        }

        // Prevent the player from going super fast when near the mirror.
        float maxHorizontalPlayerSpeed = Utils.Remap(distanceFromMirror, 600f, 150f, 11f, 0.5f);
        Target.velocity.X = Clamp(Target.velocity.X, -maxHorizontalPlayerSpeed, maxHorizontalPlayerSpeed);

        // Make sounds and music quieter the close the player is to the mirror.
        MusicVolumeManipulationSystem.MuffleFactor = Utils.Remap(distanceFromMirror, 540f, 100f, 1f, 0.01f);
        SoundMufflingSystem.MuffleFactor = Utils.Remap(distanceFromMirror, 560f, 120f, 1f, 0f);
    }

    #endregion AI

    #region Drawing

    public override void DrawBehind(int index)
    {
        // Ensure that player draw code is done before this NPC is drawn, so that using the player target doesn't cause one-frame disparities.
        Main.instance.DrawCacheNPCsOverPlayers.Add(index);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Matrix transformation = NPC.IsABestiaryIconDummy ? Main.UIScaleMatrix : Main.GameViewMatrix.TransformationMatrix;
        Vector2 scale = Vector2.One / new Vector2(Main.GameViewMatrix.TransformationMatrix.M11, Main.GameViewMatrix.TransformationMatrix.M22);
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, transformation);

        // The render target should always be inverted. Player draw information is already embedded into the target regardless, so FlipHorizontally means that everything
        // is reversed.
        SpriteEffects direction = SpriteEffects.FlipHorizontally;
        Vector2 drawPosition = NPC.Center - screenPos - Vector2.UnitY * (NPC.IsABestiaryIconDummy ? 0f : Main.LocalPlayer.gfxOffY) + Vector2.UnitY * NPC.gfxOffY;

        // Ensure that rotation is inverted.
        float rotation = -Main.LocalPlayer.fullRotation;

        if (NPC.IsABestiaryIconDummy)
        {
            NPC.Opacity = 1f;
            HorrorInterpolant = 1f;
            scale = Vector2.One * 0.7f;
        }

        // Apply the horror shader.
        var horrorShader = ShaderManager.GetShader("NoxusBoss.MirrorwalkerHorrorShader");
        horrorShader.TrySetParameter("contrastMatrix", GeneralScreenEffectSystem.CalculateContrastMatrix(NPC.Opacity * 2f));
        horrorShader.TrySetParameter("noiseOffsetFactor", Lerp(6f, 1f, NPC.Opacity) * HorrorInterpolant);
        horrorShader.TrySetParameter("noiseOverlayColor", Color.MediumPurple);
        horrorShader.TrySetParameter("noiseOverlayIntensityFactor", 31f);
        horrorShader.TrySetParameter("eyeColor", Color.Lerp(Color.Cyan, Color.Wheat, 0.7f));
        horrorShader.TrySetParameter("zoom", scale);
        horrorShader.TrySetParameter("horrorInterpolant", HorrorInterpolant.Squared());
        horrorShader.SetTexture(WavyBlotchNoise, 1, SamplerState.LinearWrap);
        horrorShader.Apply();

        Texture2D texture = LocalPlayerDrawManager.PlayerTarget;
        if (NPC.IsABestiaryIconDummy)
        {
            texture = InvisiblePixel;
            BestiaryTarget.Request(512, 512, 0, () =>
            {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                int owner = Main.myPlayer;
                Player other = Main.player[owner];
                Player player = Main.playerVisualClone[owner] ??= new Player();
                player.CopyVisuals(other);
                player.isFirstFractalAfterImage = true;
                player.firstFractalAfterImageOpacity = 1f;
                player.ResetEffects();
                player.ResetVisibleAccessories();
                player.UpdateDyes();
                player.DisplayDollUpdate();
                player.UpdateSocialShadow();
                player.itemAnimationMax = 0;
                player.itemAnimation = 0;
                player.itemRotation = 0f;
                player.heldProj = 0;
                player.direction = 1;
                player.itemRotation = 0f;
                player.wingFrame = 1;
                player.velocity.Y = 0.01f;
                player.PlayerFrame();
                player.socialIgnoreLight = true;
                Main.PlayerRenderer.DrawPlayer(Main.Camera, player, new Vector2(240f, 240f) + Main.screenPosition, 0f, player.Size * 0.5f, 0f, 1f);

                Main.spriteBatch.End();
            });

            if (BestiaryTarget.TryGetTarget(0, out RenderTarget2D? target) && target is not null)
                texture = target;
        }

        // Draw the player's target.
        float pulse = Main.GlobalTimeWrappedHourly * 2.1f % 1f * Pow(HorrorInterpolant, 1.7f);
        spriteBatch.Draw(texture, drawPosition, null, Color.White * NPC.Opacity, rotation, texture.Size() * 0.5f, scale, direction, 0f);
        spriteBatch.Draw(texture, drawPosition, null, Color.White * NPC.Opacity * Pow(1f - pulse, 2f), rotation, texture.Size() * 0.5f, scale * (1f + pulse * 0.9f), direction, 0f);

        if (NPC.IsABestiaryIconDummy)
            Main.spriteBatch.ResetToDefaultUI();
        else
            Main.spriteBatch.ResetToDefault();

        return false;
    }
    #endregion Drawing
}
