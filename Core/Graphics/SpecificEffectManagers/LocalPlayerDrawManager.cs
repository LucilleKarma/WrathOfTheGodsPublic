using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Core.World.Subworlds;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using NoxusBoss.Content.Buffs;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.SpecificEffectManagers;

[Autoload(Side = ModSide.Client)]
public class LocalPlayerDrawManager : ModSystem
{
    /// <summary>
    /// Whether a snapshot should be taken to <see cref="PlayerSnapshotTarget"/>.
    /// </summary>
    private static bool takeSnapshotNextFrame;

    /// <summary>
    /// The draw offset for various cached things for the player, such as cached gore and dust.
    /// </summary>
    public static Vector2 CacheDrawOffset
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether the player is currently being drawn from <see cref="PlayerTarget"/>.
    /// </summary>
    public static bool UseTargetDrawer
    {
        get;
        private set;
    }

    /// <summary>
    /// A render target that holds the player's draw information. If <see cref="UseTargetDrawer"/> is enabled, the player will be drawn via the contents of this and <see cref="ShaderDrawAction"/>.
    /// </summary>
    public static ManagedRenderTarget PlayerTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// A render target that holds a snapshot of the player's draw information. Can be updated via <see cref="TakePlayerSnapshot"/>.
    /// </summary>
    public static ManagedRenderTarget PlayerSnapshotTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The shader to draw the player with.
    /// </summary>
    public static Action? ShaderDrawAction
    {
        get;
        set;
    }

    /// <summary>
    /// The condition upon which the render target based drawing should cease if it returns false.
    /// </summary>
    public static Func<bool> StopCondition
    {
        get;
        set;
    }

    public override void OnModLoad()
    {
        On_LegacyPlayerRenderer.DrawPlayerFull += DrawWithTargetIfNecessary;
        On_PlayerDrawLayers.DrawPlayer_TransformDrawData += DrawCachesWithTargetOffset;
        RenderTargetManager.RenderTargetUpdateLoopEvent += PrepareDrawTarget;
        Main.QueueMainThreadAction(() =>
        {
            PlayerTarget = new(false, (width, height) => new(Main.instance.GraphicsDevice, 512, 512));
            PlayerSnapshotTarget = new(false, (width, height) => new(Main.instance.GraphicsDevice, 512, 512));
        });
    }

    private void PrepareDrawTarget()
    {
        if (Main.gameMenu)
            UseTargetDrawer = false;

        GraphicsDevice gd = Main.instance.GraphicsDevice;

        if (ShaderDrawAction is not null || UseTargetDrawer || takeSnapshotNextFrame)
        {
            // Ensure that the DrawPlayerFull method doesn't attempt to access the render target when trying to draw to the render target.
            UseTargetDrawer = false;

            // Prepare the render target.
            gd.SetRenderTarget(PlayerTarget);
            gd.Clear(Color.Transparent);

            // Draw the player.
            Vector2 oldPosition = Main.LocalPlayer.Center;
            Main.LocalPlayer.Center = Main.screenPosition + PlayerTarget.Size() * 0.5f;
            CacheDrawOffset = Main.LocalPlayer.Center - oldPosition;

            Main.LocalPlayer.itemLocation += CacheDrawOffset;
            Main.PlayerRenderer.DrawPlayers(Main.Camera, new Player[] { Main.LocalPlayer });
            Main.LocalPlayer.itemLocation -= CacheDrawOffset;

            // Reset the player's position.
            Main.LocalPlayer.Center = oldPosition;

            // Return to the backbuffer.
            gd.SetRenderTarget(null);

            UseTargetDrawer = true;
        }

        // If the snapshot was taken, draw the player to to PlayerSnapshotTarget.
        if (takeSnapshotNextFrame)
        {
            gd.SetRenderTarget(PlayerSnapshotTarget);
            gd.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

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
            player.Center = Vector2.One * 256f + Main.screenPosition;
            player.direction = 1;
            player.itemRotation = 0f;
            player.velocity.Y = 0f;
            player.wingFrame = 1;
            player.velocity.Y = 0.01f;
            player.PlayerFrame();
            player.hairColor = Color.Lerp(player.hairColor, Color.LightGray, 0.7f);
            player.skinColor = Color.Lerp(player.skinColor, Color.DarkGray, 0.5f);
            player.eyeColor = Color.Lerp(player.eyeColor, Color.Gray, 0.77f);
            player.socialIgnoreLight = true;
            Main.PlayerRenderer.DrawPlayer(Main.Camera, player, player.position, 0f, player.fullRotationOrigin, 0f, 1f / Main.GameViewMatrix.Zoom.X);

            Main.spriteBatch.End();
            gd.SetRenderTarget(null);

            // Disable the snapshot effect.
            takeSnapshotNextFrame = false;
        }
    }

    private static void PrepareSpritebatchForPlayers(Camera camera, Player drawPlayer)
    {
        SamplerState samplerState = camera.Sampler;
        if (drawPlayer.mount.Active && drawPlayer.fullRotation != 0f)
            samplerState = LegacyPlayerRenderer.MountedSamplerState;

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, samplerState, DepthStencilState.None, camera.Rasterizer, null, Matrix.Identity);
    }

    private void DrawWithTargetIfNecessary(On_LegacyPlayerRenderer.orig_DrawPlayerFull orig, LegacyPlayerRenderer self, Camera camera, Player drawPlayer)
    {
        // Use the player render target instead of manual drawing if a draw action is necessary.
        bool stopEffect = StopCondition?.Invoke() ?? true;
        if (ModContent.GetInstance<EndCreditsScene>().IsActive)
            stopEffect = true;

        if (stopEffect && drawPlayer.whoAmI == Main.myPlayer)
        {
            UseTargetDrawer = false;

            if (ShaderDrawAction is not null)
                ShaderDrawAction = null;
        }

        bool bossIsPresent = NamelessDeityBoss.Myself is not null || AvatarOfEmptiness.Myself is not null;
        bool canUseEffect = bossIsPresent || Main.LocalPlayer.GetValueRef<float>(InvincibilityBuff.InvincibilityBuffInterpolantVariableName).Value >= 0.001f || Main.LocalPlayer.GetValueRef<float>(EternalGardenUpdateSystem.PlayerLightFadeInInterpolantName) >= 0.001f;
        if (UseTargetDrawer && ShaderDrawAction is not null && drawPlayer.whoAmI == Main.myPlayer && canUseEffect && !stopEffect)
        {
            PrepareSpritebatchForPlayers(camera, drawPlayer);

            // Prepare the shader draw action and reset it.
            ShaderDrawAction.Invoke();
            ShaderDrawAction = null;

            Main.spriteBatch.Draw(PlayerTarget, drawPlayer.Center - Main.screenPosition, null, Color.White, 0f, PlayerTarget.Size() * 0.5f, 1f, 0, 0f);
            Main.spriteBatch.End();
            return;
        }

        orig(self, camera, drawPlayer);
    }

    private static void DrawCachesWithTargetOffset(On_PlayerDrawLayers.orig_DrawPlayer_TransformDrawData orig, ref PlayerDrawSet drawInfo)
    {
        orig(ref drawInfo);
        if (Main.gameMenu || drawInfo.drawPlayer.whoAmI != Main.myPlayer || !UseTargetDrawer)
            return;

        for (int i = 0; i < drawInfo.DustCache.Count; i++)
            Main.dust[drawInfo.DustCache[i]].position -= CacheDrawOffset;

        for (int i = 0; i < drawInfo.GoreCache.Count; i++)
            Main.gore[drawInfo.GoreCache[i]].position -= CacheDrawOffset;
    }

    public static void TakePlayerSnapshot() => takeSnapshotNextFrame = true;
}
