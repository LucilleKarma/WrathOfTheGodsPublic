using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles.Metaballs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class PaleComet : ModProjectile, IPixelatedPrimitiveRenderer, IProjOwnedByBoss<AvatarOfEmptiness>
{
    public bool Falling => Projectile.ai[0] == 1f;

    public PixelationPrimitiveLayer LayerToRenderTo => PixelationPrimitiveLayer.AfterProjectiles;

    public ref float Time => ref Projectile.ai[1];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
    }

    public override void SetDefaults()
    {
        Projectile.width = 38;
        Projectile.height = 38;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = AvatarOfEmptiness.Myself is null ? 210 : 240;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        if (Falling)
        {
            float maxFallSpeed = Clamp(Time - 75f, 0f, 300f) * 0.15f + 17f;
            Projectile.velocity.X *= 0.967f;
            Projectile.velocity.Y += 1.3f;
            if (Projectile.velocity.Y > maxFallSpeed)
                Projectile.velocity.Y = maxFallSpeed;

            Player closestPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            Projectile.tileCollide = Projectile.Center.Y >= closestPlayer.Center.Y && Time >= 32f;
        }
        else
        {
            // Add a mild amount of slithering movement.
            float slitherOffset = Sin(Time / 6.4f + Projectile.identity) * InverseLerp(10f, 25f, Time) * 6.2f;
            Vector2 perpendicularDirection = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(PiOver2);
            Projectile.Center += perpendicularDirection * slitherOffset;

            // Accelerate over time.
            if (Projectile.velocity.Length() < 26.25f)
                Projectile.velocity *= 1.0265f;
        }

        // Decide the current rotation.
        Projectile.rotation = (Projectile.position - Projectile.oldPosition).ToRotation();
        Projectile.spriteDirection = Cos(Projectile.rotation).NonZeroSign();
        if (Projectile.spriteDirection == -1)
            Projectile.rotation += Pi;

        Time++;
    }

    public override void OnKill(int timeLeft)
    {
        // Create gas particles.
        PaleAvatarBlobMetaball metaball = ModContent.GetInstance<PaleAvatarBlobMetaball>();
        for (int i = 0; i < 15; i++)
        {
            float gasSize = Projectile.width * Main.rand.NextFloat(0.32f, 1.6f);
            metaball.CreateParticle(Projectile.Center + Projectile.velocity * 2f, Main.rand.NextVector2Circular(8f, 8f), gasSize);
        }

        // Create explosion effects upon hitting the ground.
        if (Falling)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.ExplosionTeleport with { Volume = 0.27f, PitchVariance = 0.3f, Pitch = 0.45f, MaxInstances = 10 }, Projectile.Center);
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 3f);
        }
    }

    public override bool? CanDamage()
    {
        if (Falling)
            return Projectile.velocity.Y >= 0f;

        return null;
    }

    public float PaleMatterWidthFunction(float completionRatio)
    {
        float baseWidth = Projectile.width * 0.5f;
        float smoothTipCutoff = Pow(InverseLerp(0.03f, 0.15f, completionRatio), 0.6f);
        return smoothTipCutoff * baseWidth;
    }

    public Color PaleMatterColorFunction(float completionRatio)
    {
        return Projectile.GetAlpha(Color.White);
    }

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        Color edgeColor = new Color(255, 4, 23);
        if (Projectile.identity % 2 == 1)
            edgeColor = Color.DeepSkyBlue;

        ManagedShader bloodShader = ShaderManager.GetShader("NoxusBoss.PaleBlobShader");
        bloodShader.TrySetParameter("edgeColor", edgeColor);
        bloodShader.TrySetParameter("localTime", Main.GlobalTimeWrappedHourly * 0.8f + Projectile.identity * 34.32f);
        bloodShader.SetTexture(BubblyNoise, 1, SamplerState.PointWrap);

        PrimitiveSettings settings = new PrimitiveSettings(PaleMatterWidthFunction, PaleMatterColorFunction, _ => Projectile.Size * 0.5f + Projectile.velocity, Pixelate: true, Shader: bloodShader);
        PrimitiveRenderer.RenderTrail(Projectile.oldPos, settings, 37);
    }
}
