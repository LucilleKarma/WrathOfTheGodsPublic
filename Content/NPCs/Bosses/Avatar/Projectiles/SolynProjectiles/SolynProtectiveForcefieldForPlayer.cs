using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Graphics.Automators;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles.SolynProjectiles;

public class SolynProtectiveForcefieldForPlayer : ModProjectile, IProjOwnedByBoss<BattleSolyn>, IDrawsWithShader
{
    /// <summary>
    /// How long this forcefield has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// The owner of this forcefield.
    /// </summary>
    public Player Owner => Main.player[(int)Projectile.ai[1]];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 440;
        Projectile.height = 440;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = 999999;
        Projectile.Opacity = 0f;
    }

    public override void AI()
    {
        if (!Owner.active || Owner.dead || AvatarOfEmptiness.Myself is null)
        {
            Projectile.Kill();
            return;
        }

        if (Projectile.timeLeft > 120 && NamelessDeityBoss.Myself is not null)
            Projectile.timeLeft = 120;

        Time++;
        Projectile.scale = Utils.Remap(Time, 0f, 25f, 2f, Cos(TwoPi * Time / 7f) * 0.05f + 0.6f) + InverseLerp(20f, 0f, Projectile.timeLeft) * 1.1f;
        Projectile.Opacity = InverseLerp(0f, 30f, Time) * InverseLerp(0f, 20f, Projectile.timeLeft);
        Projectile.Center = Owner.Center;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        return false;
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        Vector4[] palette = HomingStarBolt.StarPalette;
        ManagedShader forcefieldShader = ShaderManager.GetShader("NoxusBoss.SolynForcefieldShader");
        forcefieldShader.SetTexture(DendriticNoiseZoomedOut.Value, 1, SamplerState.LinearWrap);
        forcefieldShader.TrySetParameter("forcefieldPalette", palette);
        forcefieldShader.TrySetParameter("forcefieldPaletteLength", palette.Length);
        forcefieldShader.TrySetParameter("shapeInstability", Distance(Projectile.scale, 1f) * 0.07f + 0.012f);
        forcefieldShader.TrySetParameter("flashInterpolant", 0f);
        forcefieldShader.TrySetParameter("bottomFlattenInterpolant", 0f);
        forcefieldShader.Apply();

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Main.spriteBatch.Draw(WhitePixel, drawPosition, null, Projectile.GetAlpha(Color.White), Projectile.rotation, WhitePixel.Size() * 0.5f, Projectile.Size / WhitePixel.Size() * Projectile.scale, 0, 0f);
    }
}
