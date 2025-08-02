using Luminance.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.MiscOPTools;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Projectiles.Typeless;

public class EmptinessSprayerHoldout : ModProjectile
{
    // This stores the sound slot of the static sound the spray makes, so it may be properly updated in terms of position.
    /// <summary>
    /// The reference to the looped sound played by this holdout bottle.
    /// </summary>
    public SlotId StaticSoundSlot;

    /// <summary>
    /// The owner of this bottle.
    /// </summary>
    public Player Owner => Main.player[Projectile.owner];

    /// <summary>
    /// How long this bottle has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 28;
        Projectile.height = 28;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.timeLeft = 7200;
        Projectile.penetrate = -1;
    }

    public override void AI()
    {
        Item heldItem = Owner.HeldMouseItem();

        // Die if no longer holding the click button or otherwise cannot use the item.
        if (!Owner.channel || Owner.dead || !Owner.active || Owner.noItems || Owner.CCed || heldItem is null)
        {
            Projectile.Kill();
            return;
        }

        // Stick to the owner.
        Projectile.Center = Owner.MountedCenter;
        AdjustPlayerValues();

        // Release sprayer gas.
        if (Main.myPlayer == Projectile.owner && Time % heldItem.useTime == heldItem.useTime - 1f && Owner.HasAmmo(heldItem))
        {
            Vector2 fireSpawnPosition = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitY) * heldItem.width * -1.5f;
            fireSpawnPosition += Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(PiOver2) * Owner.direction * -10f;

            Vector2 fireShootVelocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 7f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), fireSpawnPosition, fireShootVelocity.RotatedByRandom(0.05f), ModContent.ProjectileType<EmptinessSprayerGas>(), 0, 0f, Projectile.owner);
        }

        // Update the sound's position.
        if (SoundEngine.TryGetActiveSound(StaticSoundSlot, out ActiveSound? t) && t.IsPlaying)
            t.Position = Projectile.Center;
        else
            StaticSoundSlot = SoundEngine.PlaySound(GennedAssets.Sounds.Item.EmptinessSprayerLoop with { Volume = 0.54f }, Projectile.Center);

        Time++;
    }

    public void AdjustPlayerValues()
    {
        Projectile.timeLeft = 2;
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = 2;
        Owner.itemAnimation = 2;
        Owner.itemRotation = (Projectile.direction * Projectile.velocity).ToRotation();

        // Aim towards the mouse.
        if (Main.myPlayer == Projectile.owner)
        {
            Projectile.velocity = Projectile.SafeDirectionTo(Main.MouseWorld);
            if (Projectile.velocity != Projectile.oldVelocity)
                Projectile.netUpdate = true;
            Projectile.spriteDirection = (Projectile.velocity.X > 0f).ToDirectionInt();
        }

        // Rotate towards the mouse.
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Projectile.spriteDirection == -1)
            Projectile.rotation += Pi;
        Owner.ChangeDir(Projectile.spriteDirection);

        Projectile.Center += Projectile.velocity * 12f;

        // Update the player's arm directions to make it look as though they're holding the flamethrower.
        float frontArmRotation = Projectile.rotation + Owner.direction * -0.9f;
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
    }

    public override void OnKill(int timeLeft)
    {
        // Stop the static sound abruptly if the bottle is destroyed.
        if (SoundEngine.TryGetActiveSound(StaticSoundSlot, out ActiveSound? s) && s.IsPlaying)
            s.Stop();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        SpriteEffects direction = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        Texture2D glowmaskTexture = GennedAssets.Textures.MiscOPTools.EmptinessSprayer_SprayGlowmask.Value;

        EmptinessSprayer.DrawBottle(Main.GameViewMatrix.TransformationMatrix, Projectile.Center - Main.screenPosition, null, Color.White, glowmaskTexture.Size() * 0.5f, Projectile.scale, Projectile.rotation, direction);
        return false;
    }

    public override bool? CanDamage() => false;
}
