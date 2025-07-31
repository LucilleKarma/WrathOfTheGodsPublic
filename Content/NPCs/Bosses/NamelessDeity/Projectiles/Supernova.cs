using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.AdvancedProjectileOwnership;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;

public class Supernova : ModProjectile, IProjOwnedByBoss<NamelessDeityBoss>, IDrawsWithShader
{
    /// <summary>
    /// How long this supernova has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 25000;

    public override void SetDefaults()
    {
        Projectile.width = 200;
        Projectile.height = 200;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hostile = true;
        Projectile.timeLeft = 120;
        Projectile.netImportant = true;
        Projectile.hide = true;

        // This technically screws up the width/height values but that doesn't really matter since the supernova itself isn't meant to do damage.
        Projectile.scale = 0.001f;
    }

    public override void AI()
    {
        // No Nameless Deity? Die.
        if (!Projectile.TryGetGenericOwner(out NPC nameless) || nameless.ModNPC is not NamelessDeityBoss namelessModNPC)
        {
            Projectile.Kill();
            return;
        }

        // Grow over time.
        Projectile.scale += Utils.Remap(Projectile.scale, 1f, 28f, 0.45f, 0.08f);
        if (Projectile.scale >= 32f)
            Projectile.scale = 32f;

        // Dissipate at the end.
        Projectile.Opacity = InverseLerp(8f, 120f, Projectile.timeLeft);

        Time++;
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        behindProjectiles.Add(index);
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        ManagedShader supernovaShader = ShaderManager.GetShader("NoxusBoss.SupernovaShader");
        supernovaShader.TrySetParameter("supernovaColor1", Color.Orange.ToVector3());
        supernovaShader.TrySetParameter("supernovaColor2", Color.Red.ToVector3());
        supernovaShader.TrySetParameter("generalOpacity", Projectile.Opacity);
        supernovaShader.TrySetParameter("scale", Projectile.scale);
        supernovaShader.TrySetParameter("brightness", InverseLerp(20f, 4f, Projectile.scale) * 2f + 1.25f);
        supernovaShader.SetTexture(WavyBlotchNoise, 1);
        supernovaShader.SetTexture(DendriticNoiseZoomedOut, 2);
        supernovaShader.SetTexture(GennedAssets.Textures.Extra.Void, 3);
        supernovaShader.Apply();

        spriteBatch.Draw(InvisiblePixel, Projectile.Center - Main.screenPosition, null, Color.White * Projectile.Opacity * 0.42f, Projectile.rotation, InvisiblePixel.Size() * 0.5f, Projectile.scale * 400f, 0, 0f);
    }
}
