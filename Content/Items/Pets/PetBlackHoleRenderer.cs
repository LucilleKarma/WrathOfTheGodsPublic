using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Projectiles.Pets;
using NoxusBoss.Core.Graphics.RenderTargets;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Pets;

[Autoload(Side = ModSide.Client)]
public class PetBlackHoleRenderer : ModSystem
{
    public record BlackHoleRenderData(RenderTarget2D Target, Projectile BlackHole, int ShaderIndex);

    /// <summary>
    /// The set of black hole render targets. This exists for the purpose of allowing the black hole to have dyes applied by applying screen transformations to a color-modified render target of the screen.
    /// </summary>
    public static InstancedRequestableTarget BlackHoleTarget
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        BlackHoleTarget = new();
        Main.ContentThatNeedsRenderTargets.Add(BlackHoleTarget);
        On_TimeLogger.DetailedDrawTime += RenderBlackHoleWithDyes;
    }

    private static void RenderBlackHoleWithDyes(On_TimeLogger.orig_DetailedDrawTime orig, int detailedDrawType)
    {
        if (Main.gameMenu || detailedDrawType != 36)
        {
            orig(detailedDrawType);
            return;
        }

        bool resetTargets = false;
        List<BlackHoleRenderData> activeBlackHoleTargets = new List<BlackHoleRenderData>();
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            if (BlackHoleTarget.TryGetTarget(i, out RenderTarget2D? target) && target is not null)
            {
                if (!Main.projectile[i].active)
                {
                    resetTargets = true;
                    break;
                }

                Projectile blackHole = Main.projectile[i];
                Player? owner = blackHole.owner >= 0 && blackHole.owner < Main.maxPlayers ? Main.player[blackHole.owner] : null;
                activeBlackHoleTargets.Add(new BlackHoleRenderData(target, blackHole, owner?.cPet ?? 0));
            }
        }

        // Order targets such that larger black holes render on top.
        activeBlackHoleTargets = activeBlackHoleTargets.OrderByDescending(t => -t.BlackHole.width).ToList();

        // If a projectile died but still has an associated render target, reset the black hole targets, so that it doesn't linger on the screen continually.
        if (resetTargets)
        {
            BlackHoleTarget.Reset();
            orig(detailedDrawType);
            return;
        }

        if (activeBlackHoleTargets.Count <= 0)
        {
            orig(detailedDrawType);
            return;
        }

        // Render all black hole colorations with dye shaders applied.
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, CullOnlyScreen, null, Main.GameViewMatrix.TransformationMatrix);
        foreach (BlackHoleRenderData data in activeBlackHoleTargets)
        {
            Vector2 drawPosition = data.BlackHole.Center - Main.screenPosition - Vector2.One * data.Target.Size() * 0.5f;

            if (Main.projPet[data.BlackHole.type])
            {
                // Lie to the shader with a fake draw data instance so that the zoom factors are more realistic for dyes.
                Texture2D texture = GennedAssets.Textures.Extra.LieTextureForArmorShaders;
                DrawData fakeDrawData = new DrawData(data.Target, drawPosition, Color.White);
                GameShaders.Armor.Apply(data.ShaderIndex, data.BlackHole, fakeDrawData);
            }

            Main.spriteBatch.Draw(data.Target, drawPosition, Color.White);

            ManagedShader blackShader = ShaderManager.GetShader("NoxusBoss.BlackOnlyShader");
            blackShader.Apply();
            Main.spriteBatch.Draw(data.Target, drawPosition, Color.White);

            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        }

        Main.spriteBatch.End();

        orig(detailedDrawType);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        int blackHoleIndex = 0;
        int blackHolePetID = ModContent.ProjectileType<BlackHolePet>();
        int[] blackHoleTargetIDs = new int[5];
        float[] blackHoleRadii = new float[5];
        Vector2[] blackHolePoints = new Vector2[5];
        for (int i = 0; i < blackHolePoints.Length; i++)
        {
            blackHoleTargetIDs[i] = -1;
            blackHolePoints[i] = Vector2.One * -9999f;
            blackHoleRadii[i] = 0.0001f;
        }

        // Acquire all black hole data.
        Vector2 screenSize = ViewportSize;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.type != blackHolePetID)
                continue;

            blackHolePoints[blackHoleIndex] = (projectile.Center - Main.screenPosition) / screenSize;
            blackHoleRadii[blackHoleIndex] = projectile.width * projectile.scale / screenSize.X * 0.75f;
            blackHoleTargetIDs[blackHoleIndex] = projectile.whoAmI;
            blackHoleIndex++;
            if (blackHoleIndex >= blackHolePoints.Length)
                break;
        }

        // Do nothing if there are no black holes to render.
        ManagedScreenFilter distortionShader = ShaderManager.GetFilter("NoxusBoss.BlackHoleDistortionShader");
        if (blackHoleIndex <= 0)
        {
            distortionShader.Deactivate();
            if (distortionShader.Opacity > 0f)
            {
                for (int i = 0; i < 50; i++)
                    distortionShader.Update();
            }

            return;
        }

        // Apply black hole distortion effects.
        distortionShader.TrySetParameter("sourceRadii", blackHoleRadii);
        distortionShader.TrySetParameter("distortionStrength", 1f);
        distortionShader.TrySetParameter("aspectRatioCorrectionFactor", new Vector2(screenSize.X / screenSize.Y, 1f));
        distortionShader.TrySetParameter("maxLensingAngle", 28.3f);
        distortionShader.TrySetParameter("sourcePositions", blackHolePoints);
        distortionShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
        distortionShader.SetTexture(GennedAssets.Textures.Extra.PsychedelicWingTextureOffsetMap, 1, SamplerState.LinearWrap);
        distortionShader.Activate();

        // Prepare render target coloration data for later.
        foreach (int blackHoleTargetID in blackHoleTargetIDs)
        {
            if (blackHoleTargetID == -1)
                continue;

            int index = Array.IndexOf(blackHoleTargetIDs, blackHoleTargetID);
            Projectile projectile = Main.projectile[blackHoleTargetID];

            Vector2 blackHoleTargetSize = Vector2.One * 256f;
            float blackHoleResizingScale = projectile.width / blackHoleTargetSize.X * projectile.scale * 2f;
            Vector2 zoom = Vector2.One * blackHoleResizingScale;

            BlackHoleTarget.Request((int)blackHoleTargetSize.X, (int)blackHoleTargetSize.Y, blackHoleTargetID, () =>
            {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                Vector2 actualScreenSize = new Vector2(Main.screenWidth, Main.screenHeight);
                Vector3 blackHolePositionUV = Vector3.Zero;

                ManagedShader blackHoleShader = ShaderManager.GetShader("NoxusBoss.RealBlackHoleShader");
                blackHoleShader.TrySetParameter("blackHoleRadius", 0.3f);
                blackHoleShader.TrySetParameter("blackHoleCenter", blackHolePositionUV);
                blackHoleShader.TrySetParameter("aspectRatioCorrectionFactor", 1f);
                blackHoleShader.TrySetParameter("accretionDiskColor", new Color(245, 105, 61).ToVector3()); // Blue: new Color(90, 126, 210).ToVector3()
                blackHoleShader.TrySetParameter("cameraAngle", 0.32f);
                blackHoleShader.TrySetParameter("cameraRotationAxis", new Vector3(projectile.velocity.Y * -0.022f + 1f, 0f, projectile.rotation));
                blackHoleShader.TrySetParameter("accretionDiskScale", new Vector3(1f, 0.33f, 1f));
                blackHoleShader.TrySetParameter("zoom", zoom);
                blackHoleShader.TrySetParameter("accretionDiskRadius", projectile.scale * 0.4f);
                blackHoleShader.SetTexture(FireNoiseB, 1, SamplerState.LinearWrap);
                blackHoleShader.Apply();

                Texture2D background = InvisiblePixel;
                DrawData screenData = new DrawData(background, ViewportSize * 0.5f, null, Color.White, 0f, background.Size() * 0.5f, ViewportSize / background.Size(), 0, 0f);
                screenData.Draw(Main.spriteBatch);
                Main.spriteBatch.End();
            });
        }
    }
}
