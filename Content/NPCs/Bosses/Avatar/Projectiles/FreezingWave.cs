using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.Buffs;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class FreezingWave : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>, IDrawsWithShader
{
    /// <summary>
    /// The current radius of this wave.
    /// </summary>
    public ref float Radius => ref Projectile.ai[0];

    /// <summary>
    /// How long, in frames, this wave should exist before disappearing.
    /// </summary>
    public static int Lifetime => SecondsToFrames(1.32f);

    /// <summary>
    /// The maximum radius of this wave.
    /// </summary>
    public static float MaxRadius => AvatarOfEmptiness.GetAIFloat("FreezingWaveMaxRadius");

    /// <summary>
    /// The perspective rotation that applies to this wave.
    /// </summary>
    public static readonly Quaternion Rotation = Quaternion.CreateFromRotationMatrix(Matrix.CreateRotationX(0.2f));

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = Lifetime;
    }

    public override void AI()
    {
        Radius = Lerp(Radius, MaxRadius, 0.04f);
        Projectile.Opacity = InverseLerp(0f, 36f, Projectile.timeLeft);

        for (int i = 0; i < Projectile.Opacity.Squared() * 10f; i++)
            AvatarOfEmptiness.DoBehavior_AbsoluteZeroOutburst_CreateGleam(Projectile.Center, Radius * 0.75f, 30, 180);

        for (int i = 0; i < Projectile.Opacity.Squared() * 21f; i++)
        {
            Vector2 particleSpawnPosition = Projectile.Center + Main.rand.NextVector2Unit() * Radius * Projectile.scale * Main.rand.NextFloat(0.4f, 0.8f);
            Vector2 particleVelocity = (particleSpawnPosition - Projectile.Center).SafeNormalize(Vector2.UnitY).RotatedBy(PiOver2) * Main.rand.NextFloat(3f, 17f);
            SquishyLightParticle particle = new SquishyLightParticle(particleSpawnPosition, particleVelocity, Main.rand.NextFloat(0.24f, 0.7f), Color.Lerp(Color.Wheat, Color.Turquoise, Main.rand.NextFloat(0.8f)), Main.rand.Next(25, 75));
            particle.Spawn();
        }

        float squish = Vector2.Transform(Vector2.UnitY, Rotation).Y;
        foreach (Player player in Main.ActivePlayers)
        {
            Vector2 playerOffset = player.Center - Projectile.Center;
            float distanceToPlayer = Sqrt(playerOffset.X.Squared() + (playerOffset.Y * squish).Squared());
            if (Projectile.Opacity >= 0.7f && distanceToPlayer <= Radius * 0.78f && !player.HasBuff<Glaciated>() && Radius <= 2800f)
                player.AddBuff(ModContent.BuffType<Glaciated>(), SecondsToFrames(8f));
        }
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        Texture2D texture = DendriticNoiseZoomedOut.Value;
        ManagedShader shockwaveShader = ShaderManager.GetShader("NoxusBoss.FreezingWaveShader");
        shockwaveShader.TrySetParameter("globalTime", Main.GlobalTimeWrappedHourly);
        shockwaveShader.TrySetParameter("glowIntensityFactor", InverseLerp(Lifetime - 13f, Lifetime, Projectile.timeLeft) * 3f);
        shockwaveShader.SetTexture(BurnNoise.Value, 0, SamplerState.LinearWrap);
        shockwaveShader.SetTexture(WavyBlotchNoise.Value, 2, SamplerState.LinearWrap);

        float diameter = Radius * 2f;
        Vector2 drawOffset = Vector2.Transform(Vector2.One * diameter * new Vector2(-0.5f, 0.5f), Rotation);
        Vector2 drawPosition = Projectile.Center + drawOffset;
        PrimitiveRenderer.RenderQuad(texture, drawPosition, diameter / texture.Width, 0f, Projectile.GetAlpha(Color.White), shockwaveShader, Rotation);
    }
}
