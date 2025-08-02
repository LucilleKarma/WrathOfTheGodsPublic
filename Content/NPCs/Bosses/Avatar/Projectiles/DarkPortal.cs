using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Particles.Metaballs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;

public class DarkPortal : ModProjectile, IProjOwnedByBoss<AvatarOfEmptiness>
{
    public enum PortalAttackAction
    {
        Nothing,
        ShootWavingComets,
        ReleaseOtherworldlyThorns,
        StrikingArm,
        AntimatterBlasts,

        TeleportPlayerThroughPortal,
        TeleportPlayerOutOfPortal,
    }

    public PortalAttackAction Action
    {
        get => (PortalAttackAction)Projectile.ai[2];
        set => Projectile.ai[2] = (int)value;
    }

    /// <summary>
    /// The maximum scale that this portal should grow to.
    /// </summary>
    public float MaxScale => Projectile.ai[0];

    /// <summary>
    /// How long this portal has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.localAI[0];

    /// <summary>
    /// How long this portal should exist for, in frames.
    /// </summary>
    public ref float Lifetime => ref Projectile.ai[1];

    /// <summary>
    /// How many updates should be performed by each portal for each projectile update frame.
    /// </summary>
    public static int MaxUpdates => 1;

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 600;
        Projectile.height = 600;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 9600;
        Projectile.MaxUpdates = MaxUpdates;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        Time++;

        // Decide the current scale.
        Projectile.scale = InverseLerpBump(0f, MaxUpdates * 35f, Lifetime - MaxUpdates * 25f, Lifetime, Time);
        Projectile.Opacity = Pow(Projectile.scale, 2.6f);
        Projectile.rotation = Projectile.velocity.ToRotation();

        if (Time >= Lifetime)
            Projectile.Kill();

        // Shoot projectiles if the Avatar's rift is present or the Entropic God is using its dedicated portal attack.
        bool attacking = Action != PortalAttackAction.Nothing && Action != PortalAttackAction.TeleportPlayerOutOfPortal && Action != PortalAttackAction.TeleportPlayerThroughPortal;
        Player closestPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];

        if (Time == (int)(Lifetime * MaxUpdates * 0.15f) - 10f && Action == PortalAttackAction.ReleaseOtherworldlyThorns)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                float minReach = AvatarOfEmptiness.GetAIFloat("ReleaseOtherworldlyThorns_MinimumReach");
                float maxReach = AvatarOfEmptiness.GetAIFloat("ReleaseOtherworldlyThorns_MaximumReach");
                float thornReach = Main.rand.NextFloat(minReach, maxReach);
                Vector2 thornSpawnPosition = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 42f;
                NewProjectileBetter(Projectile.GetSource_FromThis(), thornSpawnPosition, Projectile.velocity.SafeNormalize(Vector2.Zero), ModContent.ProjectileType<OtherworldlyThorn>(), AvatarOfEmptiness.CometDamage, 0f, -1, thornReach);
            }
        }

        if (Time == (int)(Lifetime * MaxUpdates * 0.5f) - 10f && attacking)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                switch (Action)
                {
                    case PortalAttackAction.ShootWavingComets:
                        SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftShoot with { MaxInstances = 8, Volume = 0.6f }, Projectile.Center);

                        Vector2 cometShootVelocity = Projectile.velocity.RotatedByRandom(0.16f) * 3.7f;
                        NewProjectileBetter(Projectile.GetSource_FromThis(), Projectile.Center + cometShootVelocity.RotatedBy(PiOver2) * Main.rand.NextFloatDirection() * 0.9f, cometShootVelocity, ModContent.ProjectileType<PaleComet>(), AvatarOfEmptiness.CometDamage, 0f);
                        break;

                    case PortalAttackAction.AntimatterBlasts:
                        SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftShoot with { MaxInstances = 8, Volume = 0.6f }, Projectile.Center);

                        Vector2 bloodShootVelocity = Projectile.velocity.SafeNormalize(-Vector2.UnitY).RotatedByRandom(0.13f) * Main.rand.NextFloat(10f, 14f);
                        NewProjectileBetter(Projectile.GetSource_FromThis(), Projectile.Center + bloodShootVelocity.RotatedBy(PiOver2) * Main.rand.NextFloatDirection() * 0.48f, bloodShootVelocity, ModContent.ProjectileType<AntimatterBlast>(), AvatarOfEmptiness.AntimatterBlastDamage, 0f, -1);
                        break;

                    case PortalAttackAction.StrikingArm:
                        Vector2 armVelocity = Projectile.velocity.RotatedByRandom(0.15f);
                        Vector2 spawnOffset = Vector2.Zero;

                        SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.PortalHandReach with { Volume = 1.2f, MaxInstances = 0 }, Projectile.Center);

                        NewProjectileBetter(Projectile.GetSource_FromThis(), Projectile.Center + spawnOffset, armVelocity, ModContent.ProjectileType<PortalArmStrike>(), AvatarOfEmptiness.ArmStrikeDamage, 0f, -1);
                        break;
                }
            }

            // Release a bunch of gas particles.
            if (Action == PortalAttackAction.ShootWavingComets)
            {
                PaleAvatarBlobMetaball metaball = ModContent.GetInstance<PaleAvatarBlobMetaball>();
                for (int i = 0; i < 30; i++)
                    metaball.CreateParticle(Projectile.Center + Main.rand.NextVector2Circular(15f, 15f), Projectile.velocity.RotatedByRandom(0.68f) * Main.rand.NextFloat(19f), Main.rand.NextFloat(13f, 56f));
            }
        }

        // Handle teleport behaviors.
        if (Action == PortalAttackAction.TeleportPlayerThroughPortal && Projectile.scale >= 0.7f)
        {
            // Teleport the player through this portal and out of the equivalent one with a TeleportPlayerOutOfPortal action if touched.
            Player playerToTeleport = Main.player[Projectile.owner];
            if (playerToTeleport.WithinRange(Projectile.Center, 120f))
            {
                // Locate the opposite portal.
                Vector2? teleportDestination = null;
                foreach (Projectile portal in Main.ActiveProjectiles)
                {
                    if (portal.type != Type || portal.owner != Projectile.owner || portal.As<DarkPortal>().Action != PortalAttackAction.TeleportPlayerOutOfPortal)
                        continue;

                    teleportDestination = portal.Center - Vector2.UnitY * 40f;
                    break;
                }

                // If an opposite portal was found, teleport to it.
                if (teleportDestination.HasValue)
                {
                    playerToTeleport.Teleport(teleportDestination.Value, TeleportationStyleID.Portal);
                    playerToTeleport.velocity = Vector2.UnitY * -32f;
                }
            }
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.spriteBatch.PrepareForShaders();

        float squish = 0.5f;
        float scaleFactor = 1f;
        Color color = new Color(77, 0, 2);
        Color edgeColor = new Color(1f, 0.08f, 0.08f);
        if (Action == PortalAttackAction.AntimatterBlasts)
        {
            color = new(29, 0, 74);
            edgeColor = new(0.4f, 0f, 0.54f);
        }

        Texture2D innerRiftTexture = GennedAssets.Textures.FirstPhaseForm.RiftInnerTexture.Value;
        Vector2 textureArea = Projectile.Size * new Vector2(1f - squish, 1f) / innerRiftTexture.Size() * MaxScale * scaleFactor * 1.4f;

        ManagedShader riftShader = ShaderManager.GetShader("NoxusBoss.DarkPortalShader");
        riftShader.TrySetParameter("time", Main.GlobalTimeWrappedHourly * 0.1f);
        riftShader.TrySetParameter("baseCutoffRadius", 0.1f);
        riftShader.TrySetParameter("swirlOutwardnessExponent", 0.151f);
        riftShader.TrySetParameter("swirlOutwardnessFactor", 3.67f);
        riftShader.TrySetParameter("vanishInterpolant", InverseLerp(1f, 0f, Projectile.scale - Projectile.identity / 13f % 0.2f));
        riftShader.TrySetParameter("edgeColor", edgeColor.ToVector4());
        riftShader.TrySetParameter("edgeColorBias", 0f);
        riftShader.SetTexture(WavyBlotchNoise, 1, SamplerState.AnisotropicWrap);
        riftShader.SetTexture(WavyBlotchNoise, 2, SamplerState.AnisotropicWrap);
        riftShader.Apply();

        Main.spriteBatch.Draw(innerRiftTexture, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(color), Projectile.rotation, innerRiftTexture.Size() * 0.5f, textureArea, 0, 0f);
        Main.spriteBatch.ResetToDefault();

        return false;
    }

    public override bool ShouldUpdatePosition() => false;
}
