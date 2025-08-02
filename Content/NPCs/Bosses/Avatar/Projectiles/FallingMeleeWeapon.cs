using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class FallingMeleeWeapon : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>
{
    public enum AvatarStickState
    {
        Nothing,
        WaitingForCollision,
        StickingToAvatar
    }

    /// <summary>
    /// The stick state of this weapon with respect to the Avatar.
    /// </summary>
    public AvatarStickState StickState
    {
        get => (AvatarStickState)Projectile.ai[0];
        set => Projectile.ai[0] = (int)value;
    }

    /// <summary>
    /// This weapon's stick offset on the Avatar.
    /// </summary>
    public Vector2 StickOffset
    {
        get;
        set;
    }

    /// <summary>
    /// How long this weapon has existed so far, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[1];

    public static int Lifetime => SecondsToFrames(360f);

    public override string Texture => GetAssetPath("Content/NPCs/Bosses/Avatar/Projectiles", Name);

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
    }

    public override void SetDefaults()
    {
        Projectile.width = 50;
        Projectile.height = 50;
        Projectile.hostile = true;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.hide = true;
        Projectile.scale = Main.rand?.NextFloat(0.23f, 0.5f) ?? 1f;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        // Ensure the Avatar is present. If he isn't, die immediately.
        if (AvatarOfEmptiness.Myself is null)
        {
            Projectile.Kill();
            return;
        }

        if (Time >= 32f)
            Projectile.velocity *= 1.03f;
        Projectile.rotation = Projectile.velocity.ToRotation();

        float _ = 0f;
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        Vector2 start = Projectile.Center - direction * Projectile.scale * 450f;
        Vector2 end = Projectile.Center + direction * Projectile.scale * 450f;
        bool playGrazeSound = Collision.CheckAABBvLineCollision(Main.LocalPlayer.Hitbox.TopLeft(), Main.LocalPlayer.Hitbox.Size(), start, end, Projectile.scale * 400f, ref _);
        bool probablyGoingToGetHit = false;
        if (Main.LocalPlayer.velocity.Length() >= 1f)
            probablyGoingToGetHit = Collision.CheckLinevLine(Main.LocalPlayer.Center, Main.LocalPlayer.Center + Main.LocalPlayer.velocity * 10f + Main.rand.NextVector2CircularEdge(1f, 1f), start, end).Length >= 1;

        if (Projectile.soundDelay <= 0 && playGrazeSound && probablyGoingToGetHit)
        {
            ScreenShakeSystem.StartShake(8f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.StakeGraze with { MaxInstances = 0 });
            Projectile.soundDelay = 60;
        }

        HandleStickState();

        Time++;
    }

    public void HandleStickState()
    {
        if (StickState == AvatarStickState.Nothing || AvatarOfEmptiness.Myself is null)
            return;

        AvatarOfEmptiness avatar = AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>();
        Projectile.scale = avatar.ZPositionScale;

        if (StickState == AvatarStickState.WaitingForCollision)
        {
            Projectile.velocity = Projectile.SafeDirectionTo(AvatarOfEmptiness.Myself.Center) * Projectile.velocity.Length();

            Vector2 avatarHitboxSize = Vector2.One * avatar.ZPositionScale * AvatarOfEmptiness.Myself.scale * 340f;
            Rectangle avatarHitbox = Utils.CenteredRectangle(AvatarOfEmptiness.Myself.Center, avatarHitboxSize);
            bool collidingWithAvatar = Projectile.Colliding(Projectile.Hitbox, avatarHitbox);
            if (collidingWithAvatar)
            {
                ScreenShakeSystem.StartShake(6.7f);

                SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.StakeImpale with { MaxInstances = 0 });
                StickState = AvatarStickState.StickingToAvatar;
                StickOffset = Projectile.Center - AvatarOfEmptiness.Myself.Center;
                Projectile.netUpdate = true;

                avatar.AntishadowOnslaught_LeftArmOffset += Main.rand.NextFloat(120f, 400f);
                avatar.AntishadowOnslaught_RightArmOffset += Main.rand.NextFloat(120f, 400f);
                avatar.AntishadowOnslaught_HeadOffset += 500f;
                avatar.NPC.velocity += Projectile.velocity * 0.056f;
                AvatarOfEmptiness.Myself.netUpdate = true;
            }
        }

        if (StickState == AvatarStickState.StickingToAvatar)
        {
            Projectile.Center = AvatarOfEmptiness.Myself.Center + StickOffset;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero);
        }
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        behindNPCsAndTiles.Add(index);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Rectangle frame = texture.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
        Main.spriteBatch.Draw(texture, drawPosition, frame, Projectile.GetAlpha(Color.White), Projectile.rotation, frame.Size() * 0.5f, Projectile.scale, 0, 0f);
        return false;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float _ = 0f;
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        Vector2 start = Projectile.Center - direction * Projectile.scale * 450f;
        Vector2 end = Projectile.Center + direction * Projectile.scale * 450f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, Projectile.width * Projectile.scale, ref _);
    }
}
