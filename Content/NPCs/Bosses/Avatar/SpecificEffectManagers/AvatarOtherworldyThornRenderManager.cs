using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;

[Autoload(Side = ModSide.Client)]
public class AvatarOtherworldyThornRenderManager : ModSystem
{
    /// <summary>
    /// Whether thorns were rendered to the <see cref="OtherwordlyThornTarget"/> last frame or not.
    /// </summary>
    public static bool ThornsWereRenderedLastFrame
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target that holds all otherworldly thorn data.
    /// </summary>
    public static ManagedRenderTarget OtherwordlyThornTarget
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        OtherwordlyThornTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
        RenderTargetManager.RenderTargetUpdateLoopEvent += PrepareTarget;

        On_Main.DrawProjectiles += RenderThorns;
    }

    private void PrepareTarget()
    {
        ThornsWereRenderedLastFrame = false;

        IEnumerable<Projectile> thorns = AllProjectilesByID(ModContent.ProjectileType<OtherworldlyThorn>());
        if (!thorns.Any())
            return;

        ThornsWereRenderedLastFrame = true;

        GraphicsDevice gd = Main.instance.GraphicsDevice;
        gd.SetRenderTarget(OtherwordlyThornTarget);
        gd.Clear(Color.Transparent);

        foreach (Projectile thorn in thorns)
            thorn.As<OtherworldlyThorn>().RenderSelf();

        gd.SetRenderTarget(null);
    }

    private void RenderThorns(On_Main.orig_DrawProjectiles orig, Main self)
    {
        orig(self);

        if (ThornsWereRenderedLastFrame)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            ManagedShader overlayShader = ShaderManager.GetShader("NoxusBoss.OtherwordlyThornOverlayShader");
            overlayShader.TrySetParameter("screenSize", OtherwordlyThornTarget.Size());
            overlayShader.Apply();

            Main.spriteBatch.Draw(OtherwordlyThornTarget, Main.screenLastPosition - Main.screenPosition, Color.Red);
            Main.spriteBatch.End();
        }
    }
}
