using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Graphics.Meshes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class FrostColumn : ModProjectile, IPixelatedPrimitiveRenderer, IProjOwnedByBoss<AvatarOfEmptiness>
{
    /// <summary>
    /// The width of this column.
    /// </summary>
    public ref float ColumnWidth => ref Projectile.ai[0];

    /// <summary>
    /// The height of this column.
    /// </summary>
    public ref float ColumnHeight => ref Projectile.ai[1];

    /// <summary>
    /// How long this column has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[2];

    /// <summary>
    /// How long this column should exist for, in frames.
    /// </summary>
    public static int Lifetime => AvatarOfEmptiness.WhirlingIceStorm_FrostColumnReleaseRate;

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
    }

    public override void SetDefaults()
    {
        Projectile.width = 540;
        Projectile.height = 4020;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;

        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        Time++;

        if (Time == 1f)
        {
            Projectile.Bottom = Projectile.Center;
            Projectile.netUpdate = true;
        }

        // Make the column expand outward.
        ColumnWidth = Sqrt(InverseLerp(0f, 25f, Time)) * Projectile.width;

        // Make the column rise upawrd.
        Projectile.Opacity = InverseLerp(0f, 30f, Time);
        ColumnHeight += (1f - Projectile.Opacity) * 200f + 88f;
        if (ColumnHeight >= Projectile.height)
            ColumnHeight = Projectile.height;

        // Make the column expand further out and dissipate as it reaches the end of its lifetime.
        float fadeOut = InverseLerp(0f, 30f, Projectile.timeLeft);
        ColumnWidth += (1f - fadeOut) * 800f;
        Projectile.Opacity *= fadeOut.Squared();
    }

    public override bool? CanDamage() => ColumnHeight >= Projectile.height * 0.9f && Projectile.Opacity >= 0.8f;

    public void RenderPixelatedPrimitives(SpriteBatch spriteBatch)
    {
        GraphicsDevice gd = Main.instance.GraphicsDevice;

        Matrix scale = Matrix.CreateTranslation(0f, 0.5f, 0f) * Matrix.CreateScale(ColumnWidth, -ColumnHeight, 1f) * Matrix.CreateTranslation(0f, -0.5f, 0f);
        Matrix world = Matrix.CreateTranslation(Projectile.Center.X - Main.screenPosition.X, Projectile.Bottom.Y - Main.screenPosition.Y, 0f);
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -ColumnWidth, ColumnWidth);

        gd.RasterizerState = RasterizerState.CullNone;

        float localTime = Main.GlobalTimeWrappedHourly * Projectile.velocity.X.NonZeroSign() + Projectile.identity * 0.19f;
        ManagedShader coreShader = ShaderManager.GetShader("NoxusBoss.FrostColumnCoreShader");
        coreShader.TrySetParameter("uWorldViewProjection", scale * world * projection);
        coreShader.TrySetParameter("localTime", localTime * 3.5f);
        coreShader.TrySetParameter("mistInterpolant", 1f);
        coreShader.TrySetParameter("generalOpacity", Projectile.Opacity);
        coreShader.TrySetParameter("endWidthFactor", InverseLerp(0f, Projectile.width, ColumnWidth));
        coreShader.TrySetParameter("mistColor", new Vector3(0.4f, 0.5f, 1.4f));
        coreShader.SetTexture(TurbulentNoise, 1, SamplerState.LinearWrap);
        coreShader.SetTexture(TurbulentNoise, 2, SamplerState.LinearWrap);
        coreShader.Apply();

        gd.SetVertexBuffer(MeshRegistry.CylinderVertices);
        gd.Indices = MeshRegistry.CylinderIndices;
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, MeshRegistry.CylinderVertices.VertexCount, 0, MeshRegistry.CylinderIndices.IndexCount / 3);

        gd.SetVertexBuffer(null);
        gd.Indices = null;
    }
}
