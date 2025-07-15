using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.HuntAuricSouls;

[Autoload(Side = ModSide.Client)]
public class NamelessDeityAuricSoulDistortionSystem : ModSystem
{
    // See the comment below, where the shader parameters are set. This seems to be related and I dare not touch this section of code with such an error lurking.
#pragma warning disable IDE0044 // Add readonly modifier
    private Vector2[] effectiveWorldPositions = new Vector2[10];
#pragma warning restore IDE0044 // Add readonly modifier

    /// <summary>
    /// The render target that holds the contents of all Nameless Deity auric soul items within the world.
    /// </summary>
    public static ManagedRenderTarget ItemTarget
    {
        get;
        set;
    }

    /// <summary>
    /// The completion ratio of the overall eye open effect.
    /// </summary>
    public static float AnimationCompletion
    {
        get;
        set;
    }

    public override void OnModLoad()
    {
        ItemTarget = new ManagedRenderTarget(true, ManagedRenderTarget.CreateScreenSizedTarget);
        RenderTargetManager.RenderTargetUpdateLoopEvent += UpdateTargetWrapper;
        On_Main.DrawItems += HandleScreenShader;
    }

    private void UpdateTargetWrapper()
    {
        int itemID = ModContent.ItemType<NamelessAuricSoul>();
        var items = Main.item.Take(Main.maxItems).Where(i => i != null && i.active && i.type == itemID); //here
        if (!items.Any() && AnimationCompletion <= 0f)
            return;

        GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;
        graphicsDevice.SetRenderTarget(ItemTarget);
        graphicsDevice.Clear(Color.Transparent);

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        foreach (Item item in items)
        {
            if (item.ModItem is NamelessAuricSoul soul)
                soul.DrawSelfInWorld();
        }
        Main.spriteBatch.End();
    }

    private void HandleScreenShader(On_Main.orig_DrawItems orig, Main self)
    {
        orig(self);

        int itemID = ModContent.ItemType<NamelessAuricSoul>();
        var items = Main.item.Take(Main.maxItems).Where(i => i != null && i.active && i.type == itemID); //here
        bool anySouls = items.Any();
        AnimationCompletion = Saturate(AnimationCompletion + anySouls.ToDirectionInt() / 129f);

        if (!anySouls && AnimationCompletion <= 0f)
            return;

        Vector2[] worldPositions = items.Take(10).Select(i =>
        {
            return Vector2.Transform(i.position - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
        }).ToArray();

        for (int i = 0; i < effectiveWorldPositions.Length; i++)
        {
            if (AnimationCompletion >= 1f || anySouls)
                effectiveWorldPositions[i] = i < worldPositions.Length ? worldPositions[i] : (Vector2.One * 99999f);
        }

        float distortionStrength = InverseLerp(0.4f, 1f, NamelessAuricSoul.OpenEyeInterpolant);
        float distortionAreaFactor = SmoothStep(75f, 120f, Convert01To010(NamelessAuricSoul.OpenEyeInterpolant));

        ManagedScreenFilter overlayShader = ShaderManager.GetFilter("NoxusBoss.NamelessDeityLoreItemDistortionShader");

        // This change, which occured when making effectiveWorldPositions a field (Use git blame if you need to figure out when this was done, exactly), seemingly
        // causes the game to crash with an enigmatic access violation exception without this ToArray call. I'm not 100% sure why, but if you start seeing obscure crashes, particularly in contexts
        // pertaining to "I just beat Nameless Deity and entered my world and the game closed!" (meaning the auric soul probably dropped somewhere and this ran), I'd investigate this first, and potentially revert.
        overlayShader.TrySetParameter("loreItemPositions", effectiveWorldPositions.ToArray());
        overlayShader.TrySetParameter("screenOffset", (Main.screenPosition - Main.screenLastPosition) / Main.ScreenSize.ToVector2());
        overlayShader.TrySetParameter("oldScreenPosition", Main.screenLastPosition);
        overlayShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
        overlayShader.TrySetParameter("distortionStrength", distortionStrength);
        overlayShader.TrySetParameter("distortionAreaFactor", distortionAreaFactor);
        overlayShader.SetTexture(ItemTarget, 1, SamplerState.LinearClamp);
        overlayShader.Activate();
    }
}
