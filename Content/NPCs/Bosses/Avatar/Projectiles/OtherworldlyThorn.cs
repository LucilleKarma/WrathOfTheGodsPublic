using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Graphics.Meshes;
using NoxusBoss.Core.Graphics.ScreenShake;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class OtherworldlyThorn : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>
{
    /// <summary>
    /// The X frame of this rubble.
    /// </summary>
    public ref float MaximumReach => ref Projectile.ai[0];

    /// <summary>
    /// The Y frame of this rubble.
    /// </summary>
    public ref float CurrentReach => ref Projectile.ai[1];

    /// <summary>
    /// How long this thorn has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[2];

    /// <summary>
    /// How long this thorn should exist for, in frames.
    /// </summary>
    public static int Lifetime => AvatarOfEmptiness.GetAIInt("ReleaseOtherworldlyThorns_ThornLifetime");

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2700;
    }

    public override void SetDefaults()
    {
        Projectile.width = 140;
        Projectile.height = 140;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        int reachTelegraphTime = AvatarOfEmptiness.GetAIInt("ReleaseOtherworldlyThorns_ThornReachTelegraphTime");
        int disappearTime = AvatarOfEmptiness.GetAIInt("ReleaseOtherworldlyThorns_ThornDisappearTime");
        float idealReach = Projectile.timeLeft <= disappearTime ? 0f : MaximumReach;
        float reachSpeedInterpolant = 0.19f;
        bool canPlaySounds = Distance(Main.LocalPlayer.Center.X, Projectile.Center.X) <= 200f;
        if (Time <= reachTelegraphTime)
        {
            idealReach = MaximumReach * AvatarOfEmptiness.GetAIFloat("ReleaseOtherworldlyThorns_PeekOutTelegraphLengthFactor");
            reachSpeedInterpolant = 0.07f;
        }

        if (Time == 1f && canPlaySounds)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.OtherworldlyThornTelegraph with { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew });
        if (Projectile.timeLeft == disappearTime && canPlaySounds)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.OtherworldlyThornRetract with { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew });

        if (Time == reachTelegraphTime && Distance(Main.LocalPlayer.Center.X, Projectile.Center.X) <= 200f)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.OtherworldlyThornShoot with { MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew });
            CustomScreenShakeSystem.Start(28, 42f).
                WithDirectionalBias(new Vector2(0.04f, 1f)).
                WithDistanceFadeoff(Projectile.Center);
        }

        CurrentReach = Lerp(CurrentReach, idealReach, reachSpeedInterpolant);
        Time++;
    }

    public void RenderThorn(float width, float height, Vector2 drawBottom, Vector2 cutoffPoint, Matrix rotation)
    {
        GraphicsDevice gd = Main.instance.GraphicsDevice;

        Vector2 offset = Vector2.UnitY * (MaximumReach - CurrentReach);
        Matrix scale = Matrix.CreateTranslation(0f, 0.5f, 0f) * Matrix.CreateScale(width, -height, width) * rotation * Matrix.CreateTranslation(0f, -0.5f, 0f);
        Matrix world = Matrix.CreateTranslation(drawBottom.X - Main.screenPosition.X + offset.X, drawBottom.Y - Main.screenPosition.Y + offset.Y, 0f) * Main.GameViewMatrix.TransformationMatrix;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -width * 10f, width * 10f);

        gd.RasterizerState = RasterizerState.CullNone;

        ManagedShader thornShader = ShaderManager.GetShader("NoxusBoss.OtherwordlyThornShader");
        thornShader.TrySetParameter("uWorldViewProjection", scale * world * projection);
        thornShader.TrySetParameter("rotation", rotation);
        thornShader.TrySetParameter("cutoffY", Vector2.Transform(Vector2.UnitY * (cutoffPoint - Main.screenPosition), Main.GameViewMatrix.TransformationMatrix).Y);
        thornShader.Apply();

        gd.SetVertexBuffer(MeshRegistry.CylinderVertices);
        gd.Indices = MeshRegistry.CylinderIndices;
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, MeshRegistry.CylinderVertices.VertexCount, 0, MeshRegistry.CylinderIndices.IndexCount / 3);

        gd.SetVertexBuffer(null);
        gd.Indices = null;
    }

    public void RenderSelf()
    {
        Matrix rotation = Matrix.CreateRotationY(Projectile.identity * 0.7f);
        RenderThorn(Projectile.width, MaximumReach, Projectile.Bottom, Projectile.Bottom, rotation);

        ulong seed = (ulong)Projectile.whoAmI;
        int thornCount = (int)Lerp(7f, 16f, Utils.RandomFloat(ref seed));
        for (int i = 1; i < thornCount; i++)
        {
            float zRotation = (Utils.RandomInt(ref seed, 2) == 0).ToDirectionInt() * (PiOver2 - 0.86f) + Lerp(-0.4f, 0.4f, Utils.RandomFloat(ref seed));

            float shrivelInterpolant = 1f - i / (float)(thornCount - 1f);
            float offsetY = MaximumReach * i / (float)(thornCount - 1f) * 0.84f;
            Matrix smallThornRotation = Matrix.CreateRotationZ(zRotation) * Matrix.CreateRotationY(i * 1.3f) * rotation;
            Vector2 smallThornPosition = Projectile.Bottom - Vector2.UnitY * offsetY;
            RenderThorn(Projectile.width * shrivelInterpolant * 0.9f, CurrentReach * 0.1f, smallThornPosition, Projectile.Bottom, smallThornRotation);
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float _ = 0f;
        float width = Projectile.width * CurrentReach / MaximumReach;
        if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center - Vector2.UnitY * CurrentReach * 0.5f, width * 0.7f, ref _))
            return true;
        if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center - Vector2.UnitY * CurrentReach * 0.85f, width * 0.32f, ref _))
            return true;

        return false;
    }

    public override bool? CanDamage()
    {
        int reachTelegraphTime = AvatarOfEmptiness.GetAIInt("ReleaseOtherworldlyThorns_ThornReachTelegraphTime");
        if (Time <= reachTelegraphTime)
            return false;

        if (CurrentReach <= MaximumReach * 0.3f)
            return false;

        return null;
    }

    public override bool ShouldUpdatePosition() => false;
}
