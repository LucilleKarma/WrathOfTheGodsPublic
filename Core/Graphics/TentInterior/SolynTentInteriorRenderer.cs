﻿using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.Tiles.SolynCampsite;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.World.TileDisabling;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Core.Graphics.TentInterior;

[Autoload(Side = ModSide.Client)]
public class SolynTentInteriorRenderer : ModSystem
{
    /// <summary>
    /// A countdown for use in determining whether the player has been near a tent recently.
    /// </summary>
    public static int CloseToTentTimer
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of darkness that should be rendered outside of the tent that the player is in.
    /// </summary>
    public static float OutsideDarkness
    {
        get;
        set;
    }

    /// <summary>
    /// The render target that houses all tent contents.
    /// </summary>
    public static ManagedRenderTarget TentTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target that houses all frontal tent contents.
    /// </summary>
    public static ManagedRenderTarget TentFrontTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The set of all points that denote a tent position in tile coordinates.
    /// </summary>
    public static readonly List<Point> TentPoints = [];

    public override void OnModLoad()
    {
        TentTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
        TentFrontTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
        On_Main.DrawRain += DrawTentOverlayWrapper;
        RenderTargetManager.RenderTargetUpdateLoopEvent += RenderToTentTarget;
    }

    public override void OnWorldLoad() => OutsideDarkness = 0f;

    public override void OnWorldUnload() => OutsideDarkness = 0f;

    public override void PostUpdatePlayers()
    {
        Tile playerCenterTile = Framing.GetTileSafely(Main.LocalPlayer.Center.ToTileCoordinates());
        bool inTent = playerCenterTile.HasTile && playerCenterTile.TileType == ModContent.TileType<SolynTent>();

        OutsideDarkness = Saturate(OutsideDarkness + inTent.ToDirectionInt() * 0.056f);
    }

    public override void PostDrawTiles()
    {
        if (TileDisablingSystem.TilesAreUninteractable)
            return;

        Texture2D outsideZoneTexture = InvisiblePixel;
        bool cutsceneActive = ModContent.GetContent<SolynTentVisualCutscene>().Any(c => c.IsActive);
        if (cutsceneActive && SolynTentVisualCutsceneManager.CutsceneTarget.TryGetTarget(0, out RenderTarget2D? cutsceneTarget) && cutsceneTarget is not null)
            outsideZoneTexture = cutsceneTarget;

        if (OutsideDarkness > 0f)
        {
            ManagedScreenFilter overlayShader = ShaderManager.GetFilter("NoxusBoss.SolynTentInteriorOverlayShader");
            overlayShader.TrySetParameter("darkness", OutsideDarkness);
            overlayShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
            overlayShader.TrySetParameter("screenOffset", (Main.screenPosition - Main.screenLastPosition) / Main.ScreenSize.ToVector2());
            overlayShader.SetTexture(TentTarget, 1, SamplerState.PointClamp);
            overlayShader.SetTexture(outsideZoneTexture, 2, SamplerState.PointClamp);
            overlayShader.Activate();
        }
    }

    private void RenderToTentTarget()
    {
        if (Main.gameMenu)
            return;

        if (CloseToTentTimer <= 0)
        {
            TentPoints.Clear();
            return;
        }

        CloseToTentTimer--;

        GraphicsDevice gd = Main.instance.GraphicsDevice;
        gd.SetRenderTarget(TentTarget);
        gd.Clear(Color.Transparent);

        Main.spriteBatch.Begin();

        // Draw all tent tiles to the render target.
        int tentID = ModContent.TileType<SolynTent>();
        Texture2D tentTexture = TextureAssets.Tile[tentID].Value;
        ForEachTent((x, y, t) =>
        {
            int frameX = t.TileFrameX;
            int frameY = t.TileFrameY;

            Vector2 drawPosition = new Vector2(x * 16, y * 16) - Main.screenPosition;
            drawPosition.Y += TileObjectData.GetTileData(t).DrawYOffset;
            Color lightColor = Lighting.GetColor(x, y);

            Main.spriteBatch.Draw(tentTexture, drawPosition, new(frameX, frameY, 16, 16), lightColor, 0f, Vector2.Zero, 1f, 0, 0f);
        });

        Main.spriteBatch.End();

        gd.SetRenderTarget(TentFrontTarget);
        gd.Clear(Color.Transparent);

        Main.spriteBatch.Begin();

        // Draw all tent tiles to the render target.
        Texture2D tentFrontTexture = SolynTent.FrontTexture.Value;
        ForEachTent((x, y, t) =>
        {
            int frameX = t.TileFrameX;
            int frameY = t.TileFrameY;

            Vector2 drawPosition = new Vector2(x * 16, y * 16) - Main.screenPosition;
            drawPosition.Y += TileObjectData.GetTileData(t).DrawYOffset;
            Color lightColor = Lighting.GetColor(x, y);

            Main.spriteBatch.Draw(tentFrontTexture, drawPosition, new(frameX, frameY, 16, 16), lightColor, 0f, Vector2.Zero, 1f, 0, 0f);
        });

        Main.spriteBatch.End();
    }

    private static void ForEachTent(Action<int, int, Tile> action)
    {
        int tentID = ModContent.TileType<SolynTent>();
        foreach (Point topLeft in TentPoints)
        {
            for (int x = topLeft.X; x < topLeft.X + SolynTent.Width; x++)
            {
                for (int y = topLeft.Y; y < topLeft.Y + SolynTent.Height; y++)
                {
                    Tile t = Framing.GetTileSafely(x, y);
                    if (t.TileType != tentID || !t.HasTile)
                        continue;

                    action(x, y, t);
                }
            }
        }
    }

    private static void DrawTreeRopes()
    {
        Rectangle screenArea = new Rectangle((int)(Main.screenPosition.X / 16f) - 5, (int)(Main.screenPosition.Y / 16f) - 5, (int)(Main.screenWidth / 16f) + 10, (int)(Main.screenHeight / 16f) + 10);

        int tentID = ModContent.TileType<SolynTent>();
        for (int x = screenArea.Left; x <= screenArea.Right; x++)
        {
            for (int y = screenArea.Top; y <= screenArea.Bottom; y++)
            {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.TileType != tentID || !t.HasTile || t.TileFrameX != 0 || t.TileFrameY != 0)
                    continue;

                TESolynTent? tent = FindTileEntity<TESolynTent>(x, y, SolynTent.Width, SolynTent.Height);
                if (tent is null)
                    continue;

                tent.DrawRope();
            }
        }
    }

    private void DrawTentOverlayWrapper(On_Main.orig_DrawRain orig, Main self)
    {
        orig(self);

        if (TileDisablingSystem.TilesAreUninteractable || CloseToTentTimer <= 0)
            return;

        Main.spriteBatch.Draw(TentTarget, Main.screenLastPosition - Main.screenPosition, Color.White * (1f - OutsideDarkness));
        SpecialLayeringSystem.EmptyDrawCache_NPC(SpecialLayeringSystem.DrawCacheOverTent);
        Main.spriteBatch.Draw(TentFrontTarget, Main.screenLastPosition - Main.screenPosition, Color.White * (1f - OutsideDarkness));
        DrawTreeRopes();
    }
}
