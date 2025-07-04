using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Graphics;

using Terraria;
using Terraria.UI.Chat;

namespace NoxusBoss.Content.Rarities;

public class GenesisComponentRarity : SpeciallyRenderedRarity
{
    public override Color RarityColor => Color.HotPink;

    /// <summary>
    /// The color palette for this rarity.
    /// </summary>
    public static readonly Color[] RarityPalette = new Color[]
    {
        new(127, 81, 255),
        new(255, 236, 71),
        new(240, 109, 228),
    };

    protected override void RenderRarityText(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float maxWidth, float spread, bool ui)
    {
        Matrix originalMatrix = ui ? Main.UIScaleMatrix : Main.GameViewMatrix.TransformationMatrix;
        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, originalMatrix);

        float pulse = Main.GlobalTimeWrappedHourly * 1.4f % 1f;
        Vector2 backglowScale = scale * (Vector2.One + new Vector2(0.1f, 0.5f) * pulse);
        ChatManager.DrawColorCodedStringWithShadow(sb, font, text, position, color * Pow(1f - pulse, 1.5f), rotation, origin, backglowScale, maxWidth, spread);

        ManagedShader rarityShader = ShaderManager.GetShader("NoxusBoss.GenesisComponentRarityShader");
        rarityShader.TrySetParameter("gradient", RarityPalette.Select(r => r.ToVector3()).ToArray());
        rarityShader.TrySetParameter("gradientCount", RarityPalette.Length);
        rarityShader.TrySetParameter("hueExponent", 3.1f);
        rarityShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        rarityShader.Apply();

        ChatManager.DrawColorCodedStringWithShadow(sb, font, text, position, Color.White, rotation, origin, scale, maxWidth, spread);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, originalMatrix);
    }
}
