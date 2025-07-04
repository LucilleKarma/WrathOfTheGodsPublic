using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Core.Graphics.RenderTargets;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class NamelessBlackHoleRenderer : ModSystem
{
    /// <summary>
    /// The render target responsible for holding visual data about the black hole.
    /// </summary>
    public static DownscaleOptimizedScreenTarget BlackHoleTarget
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        On_TimeLogger.DetailedDrawTime += RenderBlackHole;
    }

    private void RenderBlackHole(On_TimeLogger.orig_DetailedDrawTime orig, int detailedDrawType)
    {
        if (detailedDrawType == 36)
        {
            List<Projectile> blackHoles = AllProjectilesByID(ModContent.ProjectileType<BlackHoleHostile>()).ToList();
            if (blackHoles.Count <= 0)
            {
                orig(detailedDrawType);
                return;
            }

            BlackHoleTarget ??= new DownscaleOptimizedScreenTarget(0.385f, PrepareBlackHoleTargetAction);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, CullOnlyScreen, null, Matrix.Identity);
            BlackHoleTarget.Render(Color.White, 0);

            Main.spriteBatch.End();
        }

        orig(detailedDrawType);
    }

    private static void PrepareBlackHoleTargetAction(int identifier)
    {
        List<Projectile> blackHoles = AllProjectilesByID(ModContent.ProjectileType<BlackHoleHostile>()).ToList();
        if (blackHoles.Count <= 0)
            return;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, CullOnlyScreen);

        Projectile blackHole = blackHoles.First();

        Vector2 screenSize = ViewportSize;
        Vector2 actualScreenSize = new Vector2(Main.screenWidth, Main.screenHeight);
        Vector3 blackHolePositionUV = new Vector3((blackHole.Center - Main.screenPosition) / actualScreenSize, 0f);
        float aspectRatioCorrectionFactor = screenSize.X / screenSize.Y;
        float blackHoleResizingScale = blackHole.width / actualScreenSize.X * blackHole.scale * 2f;
        Vector2 zoom = Main.GameViewMatrix.Zoom * blackHoleResizingScale;

        // Apply the same transformations to the UV coordinates of the black hole as those performed in the shader on coordinates so that they naturally align.
        blackHolePositionUV = (blackHolePositionUV - new Vector3(0.5f, 0.5f, 0f)) * new Vector3(aspectRatioCorrectionFactor, 1f, 1f) + new Vector3(0.5f, 0.5f, 0f);
        blackHolePositionUV = blackHolePositionUV * 2f - new Vector3(1f, 1f, 0f);
        blackHolePositionUV /= new Vector3(zoom / Main.GameViewMatrix.Zoom, 1f);

        ManagedShader blackHoleShader = ShaderManager.GetShader("NoxusBoss.RealBlackHoleShader");
        blackHoleShader.TrySetParameter("blackHoleRadius", 0.3f);
        blackHoleShader.TrySetParameter("blackHoleCenter", blackHolePositionUV);
        blackHoleShader.TrySetParameter("aspectRatioCorrectionFactor", aspectRatioCorrectionFactor);
        blackHoleShader.TrySetParameter("accretionDiskColor", new Color(245, 105, 61).ToVector3()); // Blue: new Color(90, 126, 210).ToVector3()
        blackHoleShader.TrySetParameter("cameraAngle", 0.32f);
        blackHoleShader.TrySetParameter("cameraRotationAxis", new Vector3(1f, 0f, blackHole.rotation));
        blackHoleShader.TrySetParameter("accretionDiskScale", new Vector3(1f, 0.2f, 1f));
        blackHoleShader.TrySetParameter("zoom", zoom);
        blackHoleShader.TrySetParameter("accretionDiskRadius", blackHole.scale * 0.33f);
        blackHoleShader.SetTexture(FireNoiseB, 1, SamplerState.LinearWrap);
        blackHoleShader.Apply();

        Vector2 drawPosition = screenSize * 0.5f;
        Main.spriteBatch.Draw(Main.screenTarget, drawPosition, null, Color.White, 0f, Main.screenTarget.Size() * 0.5f, screenSize / Main.screenTarget.Size(), 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin();
    }
}
