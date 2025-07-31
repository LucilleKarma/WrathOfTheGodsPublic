using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class LightSlash : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    public static float BendInterpolant => 0.16f;

    public ref float Direction => ref Projectile.ai[0];

    public ref float Time => ref Projectile.ai[1];

    public static int Lifetime => SecondsToFrames(0.4f);

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 1600;
        Projectile.height = 56;
        Projectile.hostile = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = Lifetime;
    }

    public override void AI()
    {
        Projectile.Opacity = Pow(1f - Time / Lifetime, 0.75f);
        Projectile.scale = Lerp(0.7f, 1.1f, Projectile.identity % 6f / 6f) * 0.8f;

        // Increment the time.
        Time++;
    }

    public override Color? GetAlpha(Color lightColor) => Color.White * Projectile.Opacity * 1.4f;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Time < 8f)
            return false;

        float _ = 0f;
        Vector2 start = Projectile.Center - Direction.ToRotationVector2() * Projectile.width * Projectile.scale * 0.5f;
        Vector2 end = Projectile.Center + Direction.ToRotationVector2() * Projectile.width * Projectile.scale * 0.5f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, Projectile.height * 0.5f, ref _);
    }

    public float PrimitiveWidthFunction(float completionRatio)
    {
        float baseWidth = InverseLerp(0f, 6f, Projectile.timeLeft) * Projectile.height * 0.5f;
        return InverseLerp(0f, 0.45f, completionRatio) * InverseLerp(1f, 0.55f, completionRatio) * baseWidth;
    }

    // The red channel encodes brightness.
    // The green and blue channels encode distortion direction.
    // The alpha channel encodes distortion intensity.
    public Color PrimitiveColorFunction(float completionRatio)
    {
        // With the QuadraticBezier function, an interpolant of 0.5 will always be halfway to the perpendicular midpoint.
        // Written like code, its value is:
        // Vector2 middle = (start + end) * 0.5f + (Direction + PiOver2).ToRotationVector2() * Projectile.width * Projectile.scale * BendInterpolant * 0.5f;

        // Furthermore, the arc made by the Bezier uses an offset factor of 1 - x^2.
        // This information can be used to calculate the offset angle for a given interpolant.
        float bendFactor = 1f - Pow(1f - completionRatio * 2f, 2f);

        float distortionStrength = Utils.Remap(Time, 0f, 11f, 1f, 0.1f);
        Vector2 distortionDirection = (Direction + Atan(BendInterpolant) * bendFactor + Pi).ToRotationVector2();
        return new Color(Projectile.Opacity, distortionDirection.X * 0.5f + 0.5f, distortionDirection.Y * 0.5f + 0.5f, distortionStrength);
    }

    // This projectile is not drawn by default. It is only drawn to the target in the LightSlashDrawer class.
    public override bool PreDraw(ref Color lightColor) => false;

    public void DrawToTarget()
    {
        if (Time <= 1f)
            return;

        ManagedShader slashShader = ShaderManager.GetShader("NoxusBoss.GenericTrailStreak");
        slashShader.SetTexture(GennedAssets.Textures.TrailStreaks.StreakBloomLine, 1);

        // Calculate the three points that define the overall shape of the slash.
        Vector2 start = Projectile.Center - Direction.ToRotationVector2() * Projectile.width * Projectile.scale * 0.5f;
        Vector2 end = Projectile.Center + Direction.ToRotationVector2() * Projectile.width * Projectile.scale * 0.5f;
        Vector2 middle = (start + end) * 0.5f + (Direction + PiOver2).ToRotationVector2() * Projectile.width * Projectile.scale * BendInterpolant;

        // Create a bunch of points that slash across the Bezier curve created from the above three points.
        List<Vector2> slashPoints = [];
        for (int i = 0; i < 7; i++)
        {
            float interpolant = i / 6f * Utils.Remap(Time, 0f, 8f, 0.1f, 1f);
            slashPoints.Add(QuadraticBezier(start, middle, end, interpolant));
        }

        PrimitiveSettings settings = new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction, Shader: slashShader);
        PrimitiveRenderer.RenderTrail(slashPoints, settings, 14);
    }
}
