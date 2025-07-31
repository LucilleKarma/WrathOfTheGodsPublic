using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class NamelessDeityAfterimage : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    /// <summary>
    /// The base scale of this afterimage.
    /// </summary>
    public ref float BaseScale => ref Projectile.ai[0];

    /// <summary>
    /// The rotation of this afterimage.
    /// </summary>
    public ref float Rotation => ref Projectile.ai[1];

    /// <summary>
    /// How long this afterimage has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.localAI[0];

    /// <summary>
    /// How long this afterimage should exist for.
    /// </summary>
    public static int Lifetime => SecondsToFrames(TestOfResolveSystem.IsActive ? 0.2f : 0.32f);

    /// <summary>
    /// The palette that this afterimage can cycle through.
    /// </summary>
    public static readonly Palette AfterimagePalette = new Palette().
        AddColor(new Color(0, 255, 255)).
        AddColor(new Color(0, 129, 0)).
        AddColor(new Color(197, 255, 251));

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = Lifetime;
    }

    public override void AI()
    {
        // No Nameless Deity? Die.
        if (!Projectile.TryGetGenericOwner(out NPC nameless) || nameless.ModNPC is not NamelessDeityBoss namelessModNPC)
        {
            Projectile.Kill();
            return;
        }

        // Fade out.
        Projectile.Opacity = Pow(1f - Time / Lifetime, 0.6f) * namelessModNPC.AfterimageOpacityFactor;

        // Expand/collapse.
        if (BaseScale > NamelessDeityBoss.DefaultScaleFactor * 0.99f)
            Projectile.scale *= 0.96f;
        else
        {
            Projectile.scale *= 1.018f;
            Projectile.Opacity *= 0.95f;
        }

        // Slow down.
        Projectile.velocity *= 0.976f;

        // Increment the timer.
        Time++;
    }

    public void DrawSelf()
    {
        if (!Projectile.TryGetGenericOwner(out NPC nameless) || nameless.ModNPC is not NamelessDeityBoss namelessModNPC)
            return;

        if (!NamelessDeityRenderComposite.CompositeTarget.TryGetTarget(namelessModNPC.RenderComposite.TargetIdentifier, out RenderTarget2D? target) || target is null)
            return;

        // Prepare the afterimage psychedelic shader.
        ManagedShader afterimageShader = ShaderManager.GetShader("NoxusBoss.NamelessDeityPsychedelicAfterimageShader");
        afterimageShader.TrySetParameter("uScreenResolution", Main.ScreenSize.ToVector2());
        afterimageShader.TrySetParameter("warpSpeed", Time * 0.00011f);
        afterimageShader.SetTexture(TurbulentNoise, 1);
        afterimageShader.Apply();

        // Draw the target.
        float scale = Projectile.scale * BaseScale;
        float colorInterpolant = (Projectile.identity / 19f + Main.GlobalTimeWrappedHourly * 0.75f + Projectile.Center.X / 150f) % 1f;
        Color afterimageColor = AfterimagePalette.SampleColor(colorInterpolant);
        if (TestOfResolveSystem.IsActive)
            afterimageColor = Color.White;

        Main.spriteBatch.Draw(target, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(afterimageColor), Rotation, target.Size() * 0.5f, scale, 0, 0f);
    }
}
