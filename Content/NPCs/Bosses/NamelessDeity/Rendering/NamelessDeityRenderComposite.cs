using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.FormPresets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering.RenderSteps;
using NoxusBoss.Core.Graphics.RenderTargets;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;

public class NamelessDeityRenderComposite
{
    private readonly Dictionary<string, INamelessDeityRenderStep> findCache = [];

    /// <summary>
    /// A general purpose timer used by this composite for use with individual rendering steps, such as texture swaps.
    /// </summary>
    public int Time
    {
        get;
        set;
    }

    /// <summary>
    /// The identifier for the owner of this render composite.
    /// </summary>
    public int TargetIdentifier
    {
        get
        {
            int identifier = Owner.whoAmI;
            if (Owner is NPC npc)
            {
                identifier += 65536;
                if (npc.IsABestiaryIconDummy)
                    identifier = -1;
            }

            return identifier;
        }
    }

    /// <summary>
    /// The owner of this composite.
    /// </summary>
    public Entity Owner
    {
        get;
        private set;
    }

    /// <summary>
    /// The blend state that should be used when rendering this composite.
    /// </summary>
    public BlendState BlendState
    {
        get;
        set;
    }

    /// <summary>
    /// The preset used by this render composite.
    /// </summary>
    public NamelessDeityFormPreset? UsedPreset
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target responsible for the rendering of the overall composite.
    /// </summary>
    public static InstancedRequestableTarget CompositeTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The set of swappable textures owned by this composite.
    /// </summary>
    public readonly List<NamelessDeitySwappableTexture> SwappableTextures = [];

    /// <summary>
    /// The rendering steps necessary to compose the Nameless Deity.
    /// </summary>
    public readonly List<INamelessDeityRenderStep> RenderSteps = [];

    public NamelessDeityRenderComposite(Entity owner)
    {
        Owner = owner;
        BlendState = BlendState.NonPremultiplied;
        if (Owner is not NPC npc || !npc.IsABestiaryIconDummy)
            UsedPreset = NamelessDeityFormPresetRegistry.SelectFirstAvailablePreset();

        // This seems a bit nasty, honestly?
        foreach (Type t in AssemblyManager.GetLoadableTypes(ModContent.GetInstance<NoxusBoss>().Code))
        {
            if (!typeof(INamelessDeityRenderStep).IsAssignableFrom(t) || t.IsInterface)
                continue;

            INamelessDeityRenderStep step = (Activator.CreateInstance(t) as INamelessDeityRenderStep)!;
            step.Composite = this;
            step.Initialize();
            RenderSteps.Add(step);
        }

        RerollAllSwappableTextures();
        if (CompositeTarget is null)
        {
            CompositeTarget = new();
            Main.ContentThatNeedsRenderTargets.Add(CompositeTarget);
        }
    }

    /// <summary>
    /// Finds a given render step of a given type.
    /// </summary>
    public T Find<T>() where T : INamelessDeityRenderStep
    {
        string key = typeof(T).Name;
        if (findCache.TryGetValue(key, out INamelessDeityRenderStep? step))
            return (T)step;

        findCache[key] = (T)RenderSteps.FirstOrDefault(s => s is T)!;
        return (T)findCache[key];
    }

    /// <summary>
    /// Registers a new swappable texture for this composite.
    /// </summary>
    public NamelessDeitySwappableTexture RegisterSwappableTexture(string partPrefix, int totalVariants, string[]? overridingTexturePaths = null)
    {
        NamelessDeitySwappableTexture texture = new NamelessDeitySwappableTexture(partPrefix, totalVariants);
        if (overridingTexturePaths is not null)
            texture = new NamelessDeitySwappableTexture(overridingTexturePaths);

        SwappableTextures.Add(texture);

        return texture;
    }

    /// <summary>
    /// Swaps all swappable textures registered with this composite at once.
    /// </summary>
    public void RerollAllSwappableTextures()
    {
        for (int i = 0; i < SwappableTextures.Count; i++)
            SwappableTextures[i].Swap();
    }

    /// <summary>
    /// Updates this render composite.
    /// </summary>
    public void Update()
    {
        Time++;
        for (int i = 0; i < SwappableTextures.Count; i++)
            SwappableTextures[i].Update();
    }

    /// <summary>
    /// Prepares this composite for rendering.
    /// </summary>
    public void PrepareRendering()
    {
        CompositeTarget.Request(2500, 2500, TargetIdentifier, () =>
        {
            IOrderedEnumerable<INamelessDeityRenderStep> orderedSteps = RenderSteps.OrderBy(s => s.LayerIndex);
            Vector2 viewportArea = ViewportSize;

            ResetSpriteBatch(false);
            foreach (INamelessDeityRenderStep step in orderedSteps)
            {
                // Primitive rendering resets the scissor rectangle based on screen width and height, rather than viewport width and height.
                // This leads to frustrating situations where parts of the rendering process get mysteriously cut off, which drove me mad in the past when debugging the Avatar's rendering.

                // I should probably fix this at some point, since it's a weird Luminance quirk, but in the meantime this is addressed by properly resetting the scissor rectangle
                // before each step is rendered.
                Main.instance.GraphicsDevice.ScissorRectangle = new(0, 0, (int)viewportArea.X, (int)viewportArea.Y);

                step.Render(Owner, viewportArea * 0.5f);
            }

            Main.spriteBatch.End();
        });
    }

    /// <summary>
    /// Renders this composite.
    /// </summary>
    public void Render(Vector2 censorPosition, Vector2 drawPosition, float rotation, float scale)
    {
        PrepareRendering();

        if (!CompositeTarget.TryGetTarget(TargetIdentifier, out RenderTarget2D? target) || target is null)
            return;

        Main.EntitySpriteDraw(target, drawPosition, null, Color.White, rotation, target.Size() * 0.5f, scale, 0);

        // Draw the censor if necessary.
        if (UsedPreset?.Data.UseCensor ?? true)
            RenderCensor(censorPosition, true);
    }

    /// <summary>
    /// Prepares rendering for Nameless' censor on this composite.
    /// </summary>
    public void RenderCensor(Vector2 censorPosition, bool renderImmediately)
    {
        Texture2D censor = WhitePixel;
        if (UsedPreset?.Data.CensorReplacementTexture is not null)
            censor = ModContent.Request<Texture2D>(UsedPreset.Data.CensorReplacementTexture).Value;

        Color censorColor = censor == WhitePixel.Value ? Color.Black : Color.White;
        Vector2 censorScale = new Vector2(268f, 364f) / censor.Size();

        if (Owner is NPC npc)
        {
            censorScale *= npc.scale;
            if (npc.Opacity <= 0f)
                return;
        }
        if (Owner is Projectile projectile)
            censorScale *= projectile.scale;

        // lmao.
        if (NamelessDeityFormPresetRegistry.UsingFluffyPreset)
            censorScale *= 2.5f;

        Vector2 censorDrawPosition = censorPosition + Owner.velocity;
        DrawData censorData = new DrawData(censor, censorDrawPosition, null, censorColor, 0f, censor.Size() * 0.5f, censorScale, 0, 0f);

        if (!renderImmediately)
            CensorRenderManager.Enqueue(censorData);
        else
            censorData.Draw(Main.spriteBatch);
    }

    /// <summary>
    /// Resets the sprite batch for this composite.
    /// </summary>
    public void ResetSpriteBatch(bool callEnd)
    {
        if (callEnd)
            Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }
}
