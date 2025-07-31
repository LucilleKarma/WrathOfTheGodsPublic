using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.Waters;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class PrimordialStardust : ModProjectile, IDrawsWithShader, IProjOwnedByBoss<NamelessDeityBoss>
{
    public ref float Time => ref Projectile.ai[2];

    public static int Lifetime => SecondsToFrames(2.5f);

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 750;
    }

    public override void SetDefaults()
    {
        Projectile.width = Main.rand?.Next(600, 800) ?? 400;
        Projectile.height = (int)(Projectile.width * (Main.rand?.NextFloat(0.4f, 0.75f) ?? 1f));
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.hostile = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.hide = true;
    }

    public override void AI()
    {
        // Increment time.
        Time++;

        // Grow over time.
        Projectile.scale = InverseLerp(0f, 20f, Time) + Clamp(Time - 20f, 0f, 1000f) * 0.015f;

        // Fade out.
        Projectile.Opacity = InverseLerp(0f, Lifetime * 0.45f, Projectile.timeLeft);

        // Slow down in general and ascend.
        Projectile.velocity *= 0.96f;
        Projectile.position.Y -= 4f;
    }

    // Ensure that the dark fields draw over players and NPCs.
    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        overPlayers.Add(index);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Time < 32f || Projectile.Opacity < 0.67f)
            return false;

        return null;
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        ManagedShader fogShader = ShaderManager.GetShader("NoxusBoss.PrimordialStardustShader");
        fogShader.TrySetParameter("scrollSpeed", 0.09f);
        fogShader.TrySetParameter("scrollOffset", new Vector2(Projectile.identity * 8.39317f % 1f, Projectile.identity * 9.7673f % 1f));
        fogShader.TrySetParameter("greenBias", Lerp(0.04f, 0.48f, Projectile.identity / 21f % 1f));
        fogShader.SetTexture(WavyBlotchNoise, 1, SamplerState.LinearWrap);
        fogShader.SetTexture(EternalGardenWaterShaderManager.CosmicTexture.Value, 2, SamplerState.LinearWrap);
        fogShader.Apply();

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Vector2 scale = Vector2.One * Projectile.width * Projectile.scale * 1.5f / DendriticNoiseZoomedOut.Size();
        spriteBatch.Draw(DendriticNoiseZoomedOut, drawPosition, null, Color.White * Projectile.Opacity * 0.5f, Projectile.rotation, DendriticNoiseZoomedOut.Size() * 0.5f, scale, 0, 0f);
    }
}
