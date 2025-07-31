using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.Automators;

using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles.SolynProjectiles;

public class SolynProtectiveForcefield : ModProjectile, IProjOwnedByBoss<BattleSolyn>, IDrawsWithShader
{
    /// <summary>
    /// How long this forcefield has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// How much this forcefield is currently flashing due to hit impacts.
    /// </summary>
    public ref float FlashInterpolant => ref Projectile.localAI[0];

    /// <summary>
    /// The Solyn instance who created this forcefield.
    /// </summary>
    public int SolynIndex => (int)Projectile.ai[1];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        int width = AvatarOfEmptiness.GetAIInt("BloodiedFountainBlasts_SolynForcefieldWidth");
        Projectile.width = width;
        Projectile.height = (int)(width * 0.875f);
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = 999999;
        Projectile.Opacity = 0f;
    }

    public override void AI()
    {
        NPC solyn = Main.npc[SolynIndex];
        if (!solyn.active)
        {
            Projectile.Kill();
            return;
        }

        int bloodBlobID = ModContent.ProjectileType<BloodBlob>();
        Rectangle myHitbox = Projectile.Hitbox;
        myHitbox.Y -= 35;
        myHitbox.Inflate(16, 10);

        foreach (Projectile otherProjectile in Main.ActiveProjectiles)
        {
            if (otherProjectile.hostile && otherProjectile.ModProjectile is IProjOwnedByBoss<AvatarOfEmptiness> && otherProjectile.Colliding(otherProjectile.Hitbox, myHitbox))
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.Solyn.ForcefieldHit with { Volume = 0.64f, MaxInstances = 0 }, Projectile.Center);
                if (otherProjectile.type == bloodBlobID)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 bloodVelocity = Projectile.SafeDirectionTo(otherProjectile.Center).RotatedByRandom(1.04f) * Main.rand.NextFloat(5f, 10f);
                        BloodParticle blood = new BloodParticle(otherProjectile.Center, bloodVelocity, 15, Main.rand.NextFloat(0.3f, 0.56f), Color.DarkRed);
                        blood.Spawn();
                    }
                }

                otherProjectile.Kill();
                FlashInterpolant += 0.25f;
            }
        }

        Time++;

        FlashInterpolant = Clamp(FlashInterpolant * 0.9f - 0.06f, 0f, 3f);
    }

    public override bool PreDraw(ref Color lightColor) => false;

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        Vector4[] palette = HomingStarBolt.StarPalette;
        ManagedShader forcefieldShader = ShaderManager.GetShader("NoxusBoss.SolynForcefieldShader");
        forcefieldShader.SetTexture(DendriticNoiseZoomedOut.Value, 1, SamplerState.LinearWrap);
        forcefieldShader.TrySetParameter("forcefieldPalette", palette);
        forcefieldShader.TrySetParameter("forcefieldPaletteLength", palette.Length);
        forcefieldShader.TrySetParameter("shapeInstability", Distance(Projectile.scale, 1f) * 0.24f + 0.03f);
        forcefieldShader.TrySetParameter("flashInterpolant", FlashInterpolant);
        forcefieldShader.TrySetParameter("bottomFlattenInterpolant", 1f);
        forcefieldShader.Apply();

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Main.spriteBatch.Draw(WhitePixel, drawPosition, null, Projectile.GetAlpha(Color.White), Projectile.rotation, WhitePixel.Size() * 0.5f, Projectile.Size / WhitePixel.Size() * Projectile.scale, 0, 0f);
    }
}
