using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.AvatarOfEmptiness;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class StellarRemnant : ModProjectile, IDrawsWithShader, IProjOwnedByBoss<AvatarOfEmptiness>
{
    /// <summary>
    /// The orbit offset angle of this star.
    /// </summary>
    public ref float OrbitOffsetAngle => ref Projectile.ai[0];

    /// <summary>
    /// How much the orbit of this star should be squished.
    /// </summary>
    public ref float OrbitSquish => ref Projectile.ai[1];

    /// <summary>
    /// The radius of this star's orbit.
    /// </summary>
    public ref float OrbitRadius => ref Projectile.ai[2];

    /// <summary>
    /// How long this star has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.localAI[0];

    /// <summary>
    /// The opacity factor =f this star.
    /// </summary>
    public ref float OpacityFactor => ref Projectile.localAI[1];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = Main.rand?.Next(180, 440) ?? 100;
        Projectile.height = Projectile.width;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = 60000;
        Projectile.netImportant = true;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void OnSpawn(IEntitySource source) => OpacityFactor = 1f;

    public override void AI()
    {
        // No Avatar? Die.
        if (Myself is null)
        {
            Projectile.Kill();
            return;
        }

        Projectile.scale = InverseLerp(0f, 60f, Time).Cubed();
        Projectile.Opacity = InverseLerp(0f, 45f, Time) * OpacityFactor;

        Vector2 orbitDestination = Myself.Center + OrbitOffsetAngle.ToRotationVector2() * OrbitRadius * new Vector2(1f, OrbitSquish);
        Projectile.SmoothFlyNear(orbitDestination, Projectile.Opacity * 0.04f, 1f - Projectile.Opacity * 0.13f);

        OrbitOffsetAngle += TwoPi / OrbitRadius * SmoothStep(0f, 480f, InverseLerp(0f, 90f, Time)) / Projectile.width;

        Time++;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        CircularHitboxCollision(Projectile.Center, Projectile.width * Projectile.scale * 0.5f, targetHitbox);

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        float redGiantInterpolant = InverseLerp(600f, 900f, Projectile.width) * InverseLerp(0f, 60f, Time).Squared();
        Color mainColor = Color.White;
        Color subtractiveAccentFactor = new Color(0, 30, 250);
        Color darkerColor = Color.Lerp(Color.White, Color.OrangeRed, redGiantInterpolant);
        mainColor = Color.Lerp(mainColor, new(255, 0, 0), redGiantInterpolant);
        subtractiveAccentFactor = Color.Lerp(subtractiveAccentFactor, new(255, 0, 0), redGiantInterpolant);

        float whiteInterpolant = InverseLerp(90f, 45f, Time);
        mainColor = Color.Lerp(mainColor, Color.White, whiteInterpolant);
        subtractiveAccentFactor = Color.Lerp(subtractiveAccentFactor, Color.White, whiteInterpolant);

        float spinSpeed = Utils.Remap(Projectile.width, 100f, 400f, 0.9f, 0.3f);
        ManagedShader fireballShader = ShaderManager.GetShader("NoxusBoss.StellarRemnantShader");
        fireballShader.TrySetParameter("coronaIntensityFactor", 0.09f);
        fireballShader.TrySetParameter("mainColor", mainColor);
        fireballShader.TrySetParameter("darkerColor", darkerColor);
        fireballShader.TrySetParameter("subtractiveAccentFactor", subtractiveAccentFactor);
        fireballShader.TrySetParameter("sphereSpinTime", Main.GlobalTimeWrappedHourly * spinSpeed + Projectile.identity * 1.54f);
        fireballShader.SetTexture(DendriticNoiseZoomedOut, 1);
        fireballShader.SetTexture(DendriticNoiseZoomedOut, 2);
        fireballShader.Apply();

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Vector2 scale = Vector2.One * Projectile.width * Projectile.scale * 2.1f / DendriticNoiseZoomedOut.Size();
        Main.spriteBatch.Draw(DendriticNoiseZoomedOut, drawPosition, null, Projectile.GetAlpha(Color.White), Projectile.rotation, DendriticNoiseZoomedOut.Size() * 0.5f, scale, 0, 0f);
    }

    public override bool? CanDamage() => Time >= 105f;
}
