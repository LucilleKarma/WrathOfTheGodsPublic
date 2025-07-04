using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.ItemPreRender;

/// <summary>
///     Implementing this interface allows the item to pre-render a texture for
///     the frame which all item rendering will use instead of the original
///     texture.
///     <br />
///     Useful for shaders.
/// </summary>
public interface IPreRenderedItem
{
    /// <summary>
    ///     Renders the item's texture for use for the current frame.
    /// </summary>
    /// <param name="sourceTexture">The actual texture of the item.</param>
    void PreRender(Texture2D sourceTexture);
}

/// <summary>
///     Pre-renders items each frame.  Useful for items which have shaders
///     applied for visual effects that should also apply in contexts such as
///     item use and hover.
///     <br />
///     Best if the intent is basically to create a procedural animation that
///     may be used for all instances of the item without variation.
/// </summary>
[Autoload(Side = ModSide.Client)]
public sealed class ItemPreRenderer : ModSystem
{
    private static readonly Dictionary<int, Texture2D> originalTextures = [];
    private static readonly Dictionary<int, IPreRenderedItem> preRenderedItems = [];
    private static readonly Dictionary<int, RenderTarget2D> renderTargets = [];

    private static readonly FieldInfo? assetOwnValueField = typeof(Asset<Texture2D>).GetField("ownValue", UniversalBindingFlags);

    public override void Load()
    {
        base.Load();

        if (assetOwnValueField is null)
        {
            throw new InvalidOperationException("ItemPreRenderer: Could not get Asset`1::ownValue");
        }

        On_Main.DoDraw += UpdateItemRenders;
    }

    public override void Unload()
    {
        base.Unload();

        foreach ((int itemType, Texture2D texture) in originalTextures)
        {
            ReplaceAsset(TextureAssets.Item[itemType], texture);
        }

        Main.RunOnMainThread(() =>
        {
            foreach ((_, RenderTarget2D rt) in renderTargets)
            {
                rt.Dispose();
            }
        });

        originalTextures.Clear();
        preRenderedItems.Clear();
        renderTargets.Clear();
    }

    public override void PostSetupContent()
    {
        base.PostSetupContent();

        for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
        {
            if (ItemLoader.GetItem(i) is not IPreRenderedItem preRenderedItem)
            {
                continue;
            }

            preRenderedItems[i] = preRenderedItem;

            Main.instance.LoadItem(i);
            originalTextures[i] = TextureAssets.Item[i].Value;
        }

        Main.RunOnMainThread(() =>
        {
            foreach ((int itemType, _) in preRenderedItems)
            {
                Texture2D originalTexture = originalTextures[itemType];
                var renderTarget = new RenderTarget2D(
                    Main.graphics.GraphicsDevice,
                    originalTexture.Width,
                    originalTexture.Height
                );

                renderTargets[itemType] = renderTarget;
                ReplaceAsset(TextureAssets.Item[itemType], renderTarget);
            }
        });
    }

    private static void UpdateItemRenders(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        foreach ((int itemType, IPreRenderedItem preRenderedItem) in preRenderedItems)
        {
            Texture2D originalTexture = originalTextures[itemType];
            RenderTarget2D renderTarget = renderTargets[itemType];

            Main.graphics.GraphicsDevice.SetRenderTarget(renderTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null);
            preRenderedItem.PreRender(originalTexture);
            Main.spriteBatch.End();

            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            // ReplaceAsset(TextureAssets.Item[itemType], renderTarget);
        }

        orig(self, gameTime);
    }

    private static void ReplaceAsset(Asset<Texture2D> asset, Texture2D texture)
    {
        assetOwnValueField?.SetValue(asset, texture);
    }
}
