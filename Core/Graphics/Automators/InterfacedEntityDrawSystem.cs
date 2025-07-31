using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.Automators;

[Autoload(Side = ModSide.Client)]
public class InterfacedEntityDrawSystem : ModSystem
{
    private static IEnumerable<IDrawsWithShader> projectileShaderDrawers
    {
        get
        {
            return Main.projectile.Take(Main.maxProjectiles).Where(p =>
            {
                return p.active && p.ModProjectile is IDrawsWithShader drawer && !p.IsOffscreen();
            }).Select(p => (IDrawsWithShader)p.ModProjectile);
        }
    }

    private static IEnumerable<IDrawsWithShader> npcShaderDrawers
    {
        get
        {
            return Main.npc.Take(Main.maxNPCs).Where(n =>
            {
                return n.active && n.ModNPC is IDrawsWithShader drawer;
            }).Select(n => (IDrawsWithShader)n.ModNPC);
        }
    }

    public override void Load()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        On_Main.DrawProjectiles += DrawInterfaceProjectiles;
    }

    private void DrawInterfaceProjectiles(On_Main.orig_DrawProjectiles orig, Main self)
    {
        // Collect all entities with the IDrawsWithShader and IDrawAdditive interface.
        List<IDrawsWithShader> shaderDrawers = projectileShaderDrawers.ToList();
        shaderDrawers.AddRange(npcShaderDrawers);
        shaderDrawers.OrderBy(i => i.LayeringPriority).ToList();

        List<IDrawSubtractive> subtractiveDrawers = Main.projectile.Take(Main.maxProjectiles).Where(p =>
        {
            return p.active && p.ModProjectile is IDrawSubtractive drawer && !p.IsOffscreen();
        }).Select(p => (IDrawSubtractive)p.ModProjectile).ToList();

        if (subtractiveDrawers.Count != 0)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, SubtractiveBlending, SamplerState.LinearWrap, DepthStencilState.None, DefaultRasterizerScreenCull, null, Main.GameViewMatrix.TransformationMatrix);
            DrawSubtractiveProjectiles(subtractiveDrawers);
            Main.spriteBatch.End();
        }

        // Call the base DrawProjectiles method.
        orig(self);

        // Use screen culling for optimization reasons.
        Main.instance.GraphicsDevice.ScissorRectangle = new(-5, -5, Main.screenWidth + 10, Main.screenHeight + 10);

        if (shaderDrawers.Count != 0)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, DefaultRasterizerScreenCull, null, Main.GameViewMatrix.TransformationMatrix);
            DrawShaderEntities(shaderDrawers);
            Main.spriteBatch.End();
        }
    }

    public static void DrawShaderEntities(List<IDrawsWithShader> orderedDrawers)
    {
        // Draw all projectiles that have the shader interface.
        foreach (IDrawsWithShader? drawer in orderedDrawers.Where(d => !d.ShaderShouldDrawAdditively))
            drawer.DrawWithShader(Main.spriteBatch);

        // Check for shader projectiles marked with the additive bool.
        IEnumerable<IDrawsWithShader> additiveDrawers = orderedDrawers.Where(d => d.ShaderShouldDrawAdditively);
        if (additiveDrawers.Any())
        {
            Main.spriteBatch.PrepareForShaders(BlendState.Additive);
            foreach (IDrawsWithShader? drawer in additiveDrawers)
                drawer.DrawWithShader(Main.spriteBatch);
        }
    }

    public static void DrawSubtractiveProjectiles(List<IDrawSubtractive> orderedDrawers)
    {
        // Draw all projectiles that have the subtractive interface.
        foreach (IDrawSubtractive drawer in orderedDrawers)
            drawer.DrawSubtractive(Main.spriteBatch);
    }
}
