using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.Graphics.Meshes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class CosmicMagicCircle : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>
{
    public float MagicCircleOpacity => InverseLerp(45f, 105f, Time - ConvergeTime);

    public ref float Time => ref Projectile.ai[0];

    public ref float GlowInterpolant => ref Projectile.ai[1];

    public const int ConvergeTime = 150;

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 15;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 10000;
    }

    public override void SetDefaults()
    {
        Projectile.width = 840;
        Projectile.height = 840;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.hide = true;
        Projectile.timeLeft = ConvergeTime + NamelessDeityBoss.SuperCosmicLaserbeam_LaserLifetime + 175;
    }

    public override void PostAI()
    {
        if (!Projectile.TryGetGenericOwner(out NPC nameless))
        {
            Projectile.Kill();
            return;
        }

        // Fade in.
        Projectile.scale = InverseLerp(0f, 12f, Projectile.timeLeft);
        Projectile.Opacity = InverseLerp(0f, 45f, Time) * Projectile.scale;

        // Stick to Nameless and inherit the current direction from him.
        Projectile.velocity = nameless.ai[2].ToRotationVector2();
        Projectile.Center = nameless.Center + Projectile.velocity * 380f;
        Projectile.rotation = nameless.ai[2];

        // Create charge particles.
        if (MagicCircleOpacity >= 1f && Time <= ConvergeTime + 150f)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 lightAimPosition = Projectile.Center + Projectile.velocity.RotatedBy(PiOver2) * Main.rand.NextFloatDirection() * Projectile.scale * 400f + Main.rand.NextVector2Circular(10f, 10f);
                Vector2 lightSpawnPosition = Projectile.Center + Projectile.velocity * 75f + Projectile.velocity.RotatedByRandom(1.53f) * Main.rand.NextFloat(330f, 960f);
                Vector2 lightVelocity = (lightAimPosition - lightSpawnPosition) * 0.06f;
                SquishyLightParticle light = new SquishyLightParticle(lightSpawnPosition, lightVelocity, 0.33f, Color.Pink, 19, 0.04f, 3f, 8f);
                light.Spawn();
            }
        }

        // Make the magic circle glow.
        GlowInterpolant = Saturate(GlowInterpolant + 0.1f);

        Time++;
    }

    public void DrawBloom()
    {
        Color bloomCircleColor = Projectile.GetAlpha(Color.Bisque) * 0.5f;
        Color bloomFlareColor = Projectile.GetAlpha(new(75, 33, 164)) * 0.75f;
        if (WoTGConfig.Instance.PhotosensitivityMode)
        {
            bloomCircleColor *= 0.7f;
            bloomFlareColor *= 0.5f;
        }

        Vector2 bloomDrawPosition = Projectile.Center - Main.screenPosition;

        // Draw the bloom circle.
        Main.spriteBatch.Draw(BloomCircle, bloomDrawPosition, null, bloomCircleColor, 0f, BloomCircle.Size() * 0.5f, 5f, 0, 0f);

        // Draw bloom flares that go in opposite rotations.
        float bloomFlareRotation = Main.GlobalTimeWrappedHourly * -0.4f;
        Main.spriteBatch.Draw(BloomFlare, bloomDrawPosition, null, bloomFlareColor, bloomFlareRotation, BloomFlare.Size() * 0.5f, 2f, 0, 0f);
        Main.spriteBatch.Draw(BloomFlare, bloomDrawPosition, null, bloomFlareColor, bloomFlareRotation * -0.7f, BloomFlare.Size() * 0.5f, 2f, 0, 0f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        // Draw bloom behind the book to give a nice ambient glow.
        Main.spriteBatch.UseBlendState(BlendState.Additive);
        DrawBloom();

        // Draw the magic circle.
        DrawMagicCircle();
        Main.spriteBatch.ResetToDefault();

        return false;
    }

    public void DrawMagicCircle()
    {
        // Determine draw values.
        Vector2 circleDrawPosition = Projectile.Center + Projectile.velocity * 200f - Main.screenPosition;
        Vector2 circleScale = Vector2.One * Projectile.scale * Projectile.Opacity * 1.5f;
        Color circleColor = Projectile.GetAlpha(new(92, 40, 204)) * MagicCircleOpacity;

        Texture2D magicCircleTexture = GennedAssets.Textures.Projectiles.CosmicLightCircle.Value;
        GraphicsDevice gd = Main.instance.GraphicsDevice;

        Vector2 ringDrawPosition = circleDrawPosition - Projectile.velocity * 46f;
        float ringWidth = circleScale.X * magicCircleTexture.Width;
        float ringHeight = circleScale.Y * magicCircleTexture.Height;
        Matrix rotation = Matrix.CreateRotationX(PiOver2 + 0.1f) * Matrix.CreateRotationZ(Projectile.rotation + PiOver2);
        Matrix scale = Matrix.CreateScale(ringWidth * 0.97f, -ringHeight, ringWidth * 0.35f) * rotation;
        Matrix world = Matrix.CreateTranslation(ringDrawPosition.X, ringDrawPosition.Y, 0f);
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -ringWidth, ringWidth);

        gd.RasterizerState = RasterizerState.CullNone;

        float spinRotation = Main.GlobalTimeWrappedHourly * -3.87f;
        ManagedShader ringShader = ShaderManager.GetShader("NoxusBoss.NamelessMagicCircleRingShader");
        ringShader.TrySetParameter("uWorldViewProjection", scale * world * Main.GameViewMatrix.TransformationMatrix * projection);
        ringShader.TrySetParameter("localTime", spinRotation);
        ringShader.TrySetParameter("generalColor", (circleColor with { A = 0 }) * MagicCircleOpacity);
        ringShader.TrySetParameter("glowColor", Color.White * MagicCircleOpacity);
        ringShader.SetTexture(GennedAssets.Textures.Projectiles.CosmicLightCircleRing, 1, SamplerState.LinearWrap);
        ringShader.Apply();

        gd.SetVertexBuffer(MeshRegistry.CylinderVertices);
        gd.Indices = MeshRegistry.CylinderIndices;
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, MeshRegistry.CylinderVertices.VertexCount, 0, MeshRegistry.CylinderIndices.IndexCount / 3);

        gd.SetVertexBuffer(null);
        gd.Indices = null;
        Main.spriteBatch.PrepareForShaders();

        // Apply the shader.
        ManagedShader magicCircleShader = ShaderManager.GetShader("NoxusBoss.MagicCircleShader");
        CalculatePrimitiveMatrices(Main.screenWidth, Main.screenHeight, out Matrix viewMatrix, out Matrix projectionMatrix);
        magicCircleShader.TrySetParameter("orientationRotation", Projectile.rotation);
        magicCircleShader.TrySetParameter("spinRotation", spinRotation);
        magicCircleShader.TrySetParameter("flip", Projectile.direction == -1f);
        magicCircleShader.TrySetParameter("uWorldViewProjection", viewMatrix * projectionMatrix);
        magicCircleShader.Apply();

        // Draw the circle. If the laser is present, it gains a sharp white glow.
        Texture2D magicCircleCenterTexture = GennedAssets.Textures.Projectiles.CosmicLightCircleCenter.Value;
        Main.EntitySpriteDraw(magicCircleTexture, circleDrawPosition, null, circleColor with { A = 0 }, 0f, magicCircleTexture.Size() * 0.5f, circleScale, 0, 0);
        for (float d = 0f; d < 0.03f; d += 0.01f)
            Main.EntitySpriteDraw(magicCircleTexture, circleDrawPosition, null, Color.White with { A = 0 } * GlowInterpolant * MagicCircleOpacity, 0f, magicCircleTexture.Size() * 0.5f, circleScale * (d * GlowInterpolant + 1f), 0, 0);

        // Draw the eye on top of the circle.
        magicCircleShader.TrySetParameter("spinRotation", 0f);
        magicCircleShader.Apply();
        Main.EntitySpriteDraw(magicCircleCenterTexture, circleDrawPosition, null, Color.Lerp(circleColor, Color.White * MagicCircleOpacity, 0.5f) with { A = 0 }, 0f, magicCircleCenterTexture.Size() * 0.5f, circleScale, 0, 0);
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        behindProjectiles.Add(index);
    }
}
