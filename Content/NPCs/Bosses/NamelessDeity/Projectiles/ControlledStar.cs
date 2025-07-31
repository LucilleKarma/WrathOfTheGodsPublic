using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class ControlledStar : ModProjectile, IDrawsWithShader, IProjOwnedByBoss<NamelessDeityBoss>
{
    public float LayeringPriority => 0.7f;

    /// <summary>
    /// How long this star has exist for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// The 0-1 interpolant for how unstable this star is due to being crushed.
    /// </summary>
    public ref float UnstableOverlayInterpolant => ref Projectile.ai[1];

    /// <summary>
    /// The maximum scale of this star.
    /// </summary>
    public static float MaxScale => 4.2f;

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 200;
        Projectile.height = 200;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = 60000;
        Projectile.netImportant = true;
    }

    public override void AI()
    {
        // No Nameless Deity? Die.
        if (NamelessDeityBoss.Myself is null)
            Projectile.Kill();

        Time++;

        if (UnstableOverlayInterpolant <= 0.01f)
            Projectile.scale = Pow(InverseLerp(1f, NamelessDeityBoss.SunBlenderBeams_GrowToFullSizeTime, Time), 4.1f) * MaxScale;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        CircularHitboxCollision(Projectile.Center, Projectile.width * Projectile.scale * 0.444f, targetHitbox);

    public void RenderShineGlow(Vector2 drawPosition)
    {
        ManagedShader shineShader = ShaderManager.GetShader("NoxusBoss.RadialShineShader");
        shineShader.Apply();

        Vector2 shineScale = Vector2.One * Projectile.width * Projectile.scale * 3.2f / WavyBlotchNoise.Size();
        Main.spriteBatch.Draw(WavyBlotchNoise, drawPosition, null, new Color(252, 242, 124) * 0.4f, Projectile.rotation, WavyBlotchNoise.Size() * 0.5f, shineScale, 0, 0f);
    }

    public void RenderSun(Vector2 drawPosition)
    {
        Color mainColor = Color.Lerp(new Color(204, 163, 79), new Color(100, 199, 255), Saturate(UnstableOverlayInterpolant).Cubed());
        Color darkerColor = Color.Lerp(new Color(204, 92, 25), new Color(255, 255, 255), Saturate(UnstableOverlayInterpolant).Cubed());

        ManagedShader fireballShader = ShaderManager.GetShader("NoxusBoss.SunShader");
        fireballShader.TrySetParameter("coronaIntensityFactor", UnstableOverlayInterpolant.Squared() * 1.92f + 0.044f);
        fireballShader.TrySetParameter("mainColor", mainColor);
        fireballShader.TrySetParameter("darkerColor", darkerColor);
        fireballShader.TrySetParameter("subtractiveAccentFactor", new Color(181, 0, 0));
        fireballShader.TrySetParameter("sphereSpinTime", Main.GlobalTimeWrappedHourly * 0.9f);
        fireballShader.SetTexture(WavyBlotchNoise, 1, SamplerState.LinearWrap);
        fireballShader.SetTexture(GennedAssets.Textures.Extra.PsychedelicWingTextureOffsetMap, 2, SamplerState.LinearWrap);
        fireballShader.Apply();

        Vector2 scale = Vector2.One * Projectile.width * Projectile.scale * 1.5f / DendriticNoiseZoomedOut.Size();
        Main.spriteBatch.Draw(DendriticNoiseZoomedOut, drawPosition, null, Color.White with { A = 193 }, Projectile.rotation, DendriticNoiseZoomedOut.Size() * 0.5f, scale, 0, 0f);
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        Main.spriteBatch.Draw(BloomCircleSmall, Projectile.Center - Main.screenPosition, null, Color.White with { A = 0 }, Projectile.rotation, BloomCircleSmall.Size() * 0.5f, Projectile.scale * 1.2f, 0, 0f);

        Main.spriteBatch.Draw(BloomCircleSmall, Projectile.Center - Main.screenPosition, null, new Color(50, 95, 197, 0), Projectile.rotation, BloomCircleSmall.Size() * 0.5f, UnstableOverlayInterpolant.Squared() * 9.2f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, Projectile.Center - Main.screenPosition, null, new Color(23, 105, 213, 0), Projectile.rotation, BloomCircleSmall.Size() * 0.5f, UnstableOverlayInterpolant.Squared() * 4.3f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, Projectile.Center - Main.screenPosition, null, new Color(223, 223, 255, 0), Projectile.rotation, BloomCircleSmall.Size() * 0.5f, UnstableOverlayInterpolant.Squared() * 2f, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        RenderShineGlow(drawPosition);
        RenderSun(drawPosition);

        // Draw a pure white overlay over the fireball if instructed.
        if (UnstableOverlayInterpolant >= 0.2f)
        {
            Main.spriteBatch.PrepareForShaders(BlendState.Additive);

            float glowPulse = Sin(Main.GlobalTimeWrappedHourly * UnstableOverlayInterpolant * 72f) * UnstableOverlayInterpolant * 0.56f;
            Main.spriteBatch.Draw(BloomCircle, Projectile.Center - Main.screenPosition, null, Color.White * UnstableOverlayInterpolant, Projectile.rotation, BloomCircle.Size() * 0.5f, Projectile.scale * 0.9f + glowPulse, 0, 0f);
        }

        Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        Main.spriteBatch.Draw(BloomCircleSmall, Projectile.Center - Main.screenPosition, null, new Color(255, 255, 255), Projectile.rotation, BloomCircleSmall.Size() * 0.5f, InverseLerp(0.7f, 1f, UnstableOverlayInterpolant) * 1.1f, 0, 0f);
    }

    public override bool? CanDamage() =>
        UnstableOverlayInterpolant <= 0.001f && Projectile.scale >= MaxScale * 0.9f;
}
