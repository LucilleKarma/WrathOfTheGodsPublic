using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Graphics;

using Terraria;
using Terraria.UI.Chat;

namespace NoxusBoss.Content.Rarities;

public class NamelessDeityRarity : SpeciallyRenderedRarity
{
    public override Color RarityColor => Color.White;

    protected override void RenderRarityText(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float maxWidth, float spread, bool ui)
    {
        Matrix originalMatrix = ui ? Main.UIScaleMatrix : Main.GameViewMatrix.TransformationMatrix;
        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, originalMatrix);

        ManagedShader barShader = ShaderManager.GetShader("NoxusBoss.NamelessBossBarShader");
        barShader.TrySetParameter("textureSize", Vector2.One * 560f);
        barShader.TrySetParameter("time", Main.GlobalTimeWrappedHourly * 0.4f);
        barShader.TrySetParameter("chromaticAberrationOffset", Cos01(Main.GlobalTimeWrappedHourly * 0.8f) * 4f);
        barShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        barShader.Apply();

        ChatManager.DrawColorCodedStringWithShadow(sb, font, text, position, Color.White, rotation, origin, scale, maxWidth, spread);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, originalMatrix);
    }
}
