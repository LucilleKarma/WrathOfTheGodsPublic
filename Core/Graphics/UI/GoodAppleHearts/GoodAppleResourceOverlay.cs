using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items;
using NoxusBoss.Content.Items.Dyes;
using NoxusBoss.Core.DataStructures;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.UI.GoodAppleHearts;

public class GoodAppleResourceOverlay : ModResourceOverlay
{
    private static LazyAsset<Texture2D> barsPanelTexture;

    private static LazyAsset<Texture2D> fancyPanelTexture;

    private static LazyAsset<Texture2D> heartTexture;

    // This field is used to cache vanilla assets used in the CompareAssets method.
    private readonly Dictionary<string, Asset<Texture2D>> vanillaAssetCache = [];

    private const string FancyFolder = "Images/UI/PlayerResourceSets/FancyClassic/";

    private const string BarsFolder = "Images/UI/PlayerResourceSets/HorizontalBars/";

    public static Referenced<int> TotalGoodApplesConsumed => Main.LocalPlayer.GetValueRef<int>(GoodApple.TotalApplesConsumedFieldName);

    public const int ApplesNeededForMaxIntensity = 25;

    public override void SetStaticDefaults()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        // Autoload assets.
        barsPanelTexture = LazyAsset<Texture2D>.FromPath(GetAssetPath("UI/GoodAppleHearts", "BarsLifeOverlay_Panel"));
        fancyPanelTexture = LazyAsset<Texture2D>.FromPath(GetAssetPath("UI/GoodAppleHearts", "FancyLifeOverlay_Panel"));
        heartTexture = LazyAsset<Texture2D>.FromPath(GetAssetPath("UI/GoodAppleHearts", "ClassicLifeOverlay"));
    }

    public override bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText)
    {
        drawText = true;
        if (TotalGoodApplesConsumed >= 1)
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        return true;
    }

    public override void PostDrawResource(ResourceOverlayDrawContext context)
    {
        // Do nothing if the player has not consumed any good apples.
        if (TotalGoodApplesConsumed <= 0)
            return;

        // Calculate how intense the overlay should be based on how many apples have been consumed.
        float overlayIntensity = InverseLerp(1f, ApplesNeededForMaxIntensity, TotalGoodApplesConsumed);

        // Perform drawing.
        Draw(context, overlayIntensity);
    }

    public void Draw(ResourceOverlayDrawContext context, float overlayIntensity)
    {
        // Load the draw context's texture to determine which mode should be drawn.
        Asset<Texture2D> asset = context.texture;
        bool drawingBarsPanels = CompareAssets(asset, BarsFolder + "HP_Panel_Middle");

        if (asset == TextureAssets.Heart || asset == TextureAssets.Heart2)
        {
            // Draw over the Classic hearts.
            ApplyShader(context);
            DrawOver(context, overlayIntensity, heartTexture, false);
        }
        else if (CompareAssets(asset, FancyFolder + "Heart_Fill") || CompareAssets(asset, FancyFolder + "Heart_Fill_B"))
        {
            // Draw over the Fancy hearts.
            ApplyShader(context);
            DrawOver(context, overlayIntensity, heartTexture, true);

            // Reset the shader.
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        }

        else if (CompareAssets(asset, FancyFolder + "Heart_Left") || CompareAssets(asset, FancyFolder + "Heart_Middle") || CompareAssets(asset, FancyFolder + "Heart_Right") || CompareAssets(asset, FancyFolder + "Heart_Right_Fancy") || CompareAssets(asset, FancyFolder + "Heart_Single_Fancy"))
        {
            Vector2 drawOffset;
            if (context.resourceNumber == context.snapshot.AmountOfLifeHearts - 1)
            {
                // Final panel to draw has a special "Fancy" variant.  Determine whether it has panels to the left of it
                if (CompareAssets(context.texture, FancyFolder + "Heart_Single_Fancy"))
                {
                    // First and only panel in this panel's row.
                    drawOffset = Vector2.One * 8f;
                }
                else
                {
                    // Other panels existed in this panel's row.
                    // Vanilla texture is "Heart_Right_Fancy".
                    drawOffset = Vector2.One * 8f;
                }
            }
            else if (CompareAssets(context.texture, FancyFolder + "Heart_Left"))
            {
                // First panel in this row.
                drawOffset = Vector2.One * 4f;
            }
            else if (CompareAssets(context.texture, FancyFolder + "Heart_Middle"))
            {
                // Any panel that has a panel to its left AND right.
                drawOffset = Vector2.UnitY * 4f;
            }
            else
            {
                // Final panel in the first row.
                // Vanilla texture is "Heart_Right".
                drawOffset = Vector2.UnitY * 4f;
                if (context.resourceNumber >= 10)
                    drawOffset += new Vector2(8f, 4f);
            }

            // Draw over the Fancy heart panels.
            DrawOver(context, overlayIntensity, fancyPanelTexture, false, drawOffset);
        }

        else if (drawingBarsPanels)
        {
            // Draw over the Bars middle life panels.
            DrawOver(context, overlayIntensity, barsPanelTexture, false, Vector2.UnitY * 6f);
        }
    }

    public static void ApplyShader(ResourceOverlayDrawContext context)
    {
        // Apply the numinous shader dye effect on top of the hearts.
        ManagedShader dyeShader = ShaderManager.GetShader("NoxusBoss.NuminousDyeShader");
        dyeShader.TrySetParameter("uImageSize0", heartTexture.Size());
        dyeShader.TrySetParameter("uSourceRect", heartTexture.Value.Frame());
        dyeShader.TrySetParameter("uTime", Main.GlobalTimeWrappedHourly * 0.5f - context.resourceNumber * Pi / 20f);
        dyeShader.SetTexture(NuminousDye.ShaderTexture, 1);
        dyeShader.Apply();
    }

    public static void DrawOver(ResourceOverlayDrawContext context, float overlayIntensity, LazyAsset<Texture2D> overlay, bool drawPulse, Vector2 drawOffset = default)
    {
        context.position += drawOffset;

        // Draw the custom texture over the original. It becomes increasingly opaque according to how intense the overlay should be.
        if (overlayIntensity > 0f)
        {
            Texture2D overlayTexture = overlay.Value;
            Main.spriteBatch.Draw(overlayTexture, context.position, null, Color.White * Sqrt(overlayIntensity), context.rotation, context.origin, context.scale, context.effects, 0f);

            if (drawPulse)
            {
                float pulseSpeed = Lerp(1.25f, 3.2f, overlayIntensity);
                float pulse = (Main.GlobalTimeWrappedHourly * pulseSpeed - context.resourceNumber * 0.05f) % 1f;
                Vector2 pulseScale = context.scale * Lerp(1f, 1.01f + overlayIntensity * 1.2f, pulse);
                Main.spriteBatch.Draw(overlayTexture, context.position, null, Color.White * overlayIntensity * (1f - pulse) * 0.2f, context.rotation, overlayTexture.Size() * 0.5f, pulseScale, context.effects, 0f);
            }
        }
    }

    private bool CompareAssets(Asset<Texture2D> existingAsset, string compareAssetPath)
    {
        // This is a helper method for checking if a certain vanilla asset was drawn, provided by TML's ExampleMod.
        if (!vanillaAssetCache.TryGetValue(compareAssetPath, out Asset<Texture2D>? asset))
            asset = vanillaAssetCache[compareAssetPath] = Main.Assets.Request<Texture2D>(compareAssetPath);

        return existingAsset == asset;
    }
}
