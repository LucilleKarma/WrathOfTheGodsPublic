using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Graphics.Automators;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;

public class DirectionalSolynForcefield : ModProjectile, IProjOwnedByBoss<BattleSolyn>, IDrawsWithShader
{
    /// <summary>
    /// The owner of this forcefield.
    /// </summary>
    public Player Owner => Main.player[Projectile.owner];

    /// <summary>
    /// The disappearance timer for this forcefield.
    /// </summary>
    public int DisappearTime
    {
        get;
        set;
    }

    /// <summary>
    /// The 0-1 interpolant for this forcefield's disappearance.
    /// </summary>
    public float DisappearanceInterpolant => InverseLerp(0f, 24f, DisappearTime);

    /// <summary>
    /// How long this forcefield has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// The moving average of this forcefield's spin angular velocity.
    /// </summary>
    public ref float SpinSpeedMovingAverage => ref Projectile.ai[1];

    /// <summary>
    /// The 0-1 completion value for impacts to this forcefield.
    /// </summary>
    public ref float ImpactAnimationCompletion => ref Projectile.ai[2];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
    }

    public override void SetDefaults()
    {
        int width = MarsBody.GetAIInt("BrutalBarrage_SolynForcefieldWidth");
        Projectile.width = width;
        Projectile.height = (int)(width * 0.27f);
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = 999999;
        Projectile.penetrate = -1;
        Projectile.Opacity = 0f;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(DisappearTime);
        writer.Write(Projectile.rotation);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        DisappearTime = reader.ReadInt32();
        Projectile.rotation = reader.ReadSingle();
    }

    public override void AI()
    {
        int ownerIndex = -1;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is BattleSolyn solyn && solyn.MultiplayerIndex == Owner.whoAmI)
            {
                ownerIndex = npc.whoAmI;
                break;
            }
        }

        if (ownerIndex == -1)
        {
            Projectile.Kill();
            return;
        }

        MoveTowardsMouse();
        HandleImpactAnimationTimings();

        if (DisappearTime >= 1)
            DisappearTime++;

        // Fade in and out.
        if (MarsBody.SolynEnergyBeamIsCharging(Owner))
            Projectile.Opacity = Projectile.Opacity.StepTowards(0f, 0.06f);
        else
            Projectile.Opacity = Projectile.Opacity.StepTowards(InverseLerp(0f, 16f, Time) * (1f - DisappearanceInterpolant), 0.05f);

        // Decide the current scale.
        float impactCompletionBump = Convert01To010(ImpactAnimationCompletion);
        float impactAFfectedScale = Lerp(1f, 0.6f, impactCompletionBump) + Pow(impactCompletionBump, 4f) * 0.56f;
        Projectile.scale = impactAFfectedScale + DisappearanceInterpolant * 0.75f;

        Time++;

        if (DisappearanceInterpolant >= 1f)
            Projectile.Kill();
    }

    /// <summary>
    /// Moves the forcefield towards the mouse.
    /// </summary>
    public void MoveTowardsMouse()
    {
        if (Main.myPlayer != Projectile.owner)
            return;

        float oldDirection = Projectile.velocity.ToRotation();
        float idealDirection = Owner.AngleTo(Main.MouseWorld);
        if (MarsBody.SolynEnergyBeamIsCharging(Owner))
            idealDirection = oldDirection;

        float reorientInterpolant = (1f - DisappearanceInterpolant) * (1f - GameSceneSlowdownSystem.SlowdownInterpolant);
        float newDirection = oldDirection.AngleLerp(idealDirection, reorientInterpolant * 0.25f).AngleTowards(idealDirection, reorientInterpolant * 0.008f);

        Projectile.velocity = newDirection.ToRotationVector2();
        Projectile.rotation = newDirection + PiOver2;
        if (oldDirection != newDirection)
        {
            Projectile.netUpdate = true;
            Projectile.netSpam = 0;
        }

        float angleDifference = WrapAngle(newDirection - oldDirection);
        SpinSpeedMovingAverage = Lerp(SpinSpeedMovingAverage, Abs(angleDifference), 0.03f);

        float offset = Lerp(90f, 67f, Convert01To010(ImpactAnimationCompletion));
        Projectile.Center = Owner.Center + Projectile.velocity * offset;
    }

    /// <summary>
    /// Handles the incrementing and resetting of the <see cref="ImpactAnimationCompletion"/> value.
    /// </summary>
    public void HandleImpactAnimationTimings()
    {
        if (ImpactAnimationCompletion > 0f)
        {
            ImpactAnimationCompletion += 0.09f;
            if (ImpactAnimationCompletion >= 1f)
            {
                ImpactAnimationCompletion = 0f;
                Projectile.netUpdate = true;
            }
        }
    }

    /// <summary>
    /// Initiates the disappearance of this forcefield.
    /// </summary>
    public void BeginDisappearing()
    {
        DisappearTime = 1;
        Projectile.netUpdate = true;
    }

    public void DrawWithShader(SpriteBatch spriteBatch)
    {
        ManagedShader forcefieldShader = ShaderManager.GetShader("NoxusBoss.DirectionalSolynForcefieldShader");
        forcefieldShader.TrySetParameter("colorA", new Color(255, 113, 194).ToVector4());
        forcefieldShader.TrySetParameter("colorB", Color.Wheat.ToVector4());
        forcefieldShader.TrySetParameter("glowIntensity", Lerp(0.75f, 3f, Convert01To010(ImpactAnimationCompletion)));
        forcefieldShader.SetTexture(StarDistanceLookup, 1, SamplerState.LinearClamp);
        forcefieldShader.Apply();

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Color color = Projectile.GetAlpha(Color.White);
        for (int i = 8; i >= 1; i--)
        {
            float afterimageRotation = Projectile.oldRot[i].AngleLerp(Projectile.rotation, 0.5f);
            float angularOffset = WrapAngle(afterimageRotation - Projectile.rotation);
            float afterimageOpacity = Exp(i * -0.4f);
            Vector2 afterimageDrawPosition = drawPosition.RotatedBy(angularOffset, Owner.Center - Main.screenPosition);
            Main.spriteBatch.Draw(WhitePixel, afterimageDrawPosition, null, color * afterimageOpacity, afterimageRotation, WhitePixel.Size() * 0.5f, Projectile.scale * Projectile.Size * 5f, 0, 0f);
        }

        Main.spriteBatch.Draw(WhitePixel, drawPosition, null, color, Projectile.rotation, WhitePixel.Size() * 0.5f, Projectile.scale * Projectile.Size * 5f, 0, 0f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        Utilities.RotatingHitboxCollision(Projectile, targetHitbox.TopLeft(), targetHitbox.Size()) && Projectile.Opacity >= 0.56f;

    public override bool ShouldUpdatePosition() => false;
}
