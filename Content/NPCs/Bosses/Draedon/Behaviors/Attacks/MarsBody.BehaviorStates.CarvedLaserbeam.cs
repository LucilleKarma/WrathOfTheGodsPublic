using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.World.WorldSaving;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    /// <summary>
    /// The current direction of Mars' laserbeam during his carved laserbeam attack.
    /// </summary>
    public ref float CarvedLaserbeam_LaserbeamDirection => ref NPC.ai[0];

    /// <summary>
    /// The current angular velocity of Mars' laserbeam during his carved laserbeam attack.
    /// </summary>
    public ref float CarvedLaserbeam_LaserbeamAngularVelocity => ref NPC.ai[1];

    /// <summary>
    /// How long Mars spends redirecting during his carved laserbeam attack.
    /// </summary>
    public static int CarvedLaserbeam_MarsRedirectTime => GetAIInt("CarvedLaserbeam_MarsRedirectTime");

    /// <summary>
    /// How long Mars spends charging up energy during his carved laserbeam attack.
    /// </summary>
    public static int CarvedLaserbeam_LaserChargeUpTime => GetAIInt("CarvedLaserbeam_LaserChargeUpTime");

    /// <summary>
    /// How long Mars spends firing his laser during his carved laserbeam attack.
    /// </summary>
    public static int CarvedLaserbeam_LaserShootTime => GetAIInt("CarvedLaserbeam_LaserShootTime");

    /// <summary>
    /// How much damage Mars' exoelectric disintegration ray does.
    /// </summary>
    public static int ExoelectricDisintegrationRayDamage => GetAIInt("ExoelectricDisintegrationRayDamage");

    /// <summary>
    /// How much of a safe zone is afforded to the player by Solyn's shield during Mars' carved laserbeam attack.
    /// </summary>
    public static float CarvedLaserbeam_LaserSafeZoneWidth => GetAIFloat("CarvedLaserbeam_LaserSafeZoneWidth");

    /// <summary>
    /// The width of Mars' deathray during his carved laserbeam attack.
    /// </summary>
    public static float CarvedLaserbeam_LaserWidth => GetAIFloat("CarvedLaserbeam_LaserWidth");

    [AutomatedMethodInvoke]
    public void LoadState_CarvedLaserbeam()
    {
        StateMachine.RegisterTransition(MarsAIType.CarvedLaserbeam, MarsAIType.VulnerableUntilDeath, false, () =>
        {
            return AITimer >= CarvedLaserbeam_MarsRedirectTime + CarvedLaserbeam_LaserChargeUpTime + CarvedLaserbeam_LaserShootTime + 90;
        }, () =>
        {
            int starID = ModContent.ProjectileType<SolynSentientStar>();
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.type == starID)
                    projectile.As<DirectionalSolynForcefield2>().BeginDisappearing();
            }
        });
        StateMachine.RegisterStateBehavior(MarsAIType.CarvedLaserbeam, DoBehavior_CarvedLaserbeam);
    }

    /// <summary>
    /// Performs Mars' carved laserbeam attack, making him project a red laserbeam that Solyn carves through while weaving missiles.
    /// </summary>
    public void DoBehavior_CarvedLaserbeam()
    {
        SolynAction = DoBehavior_CarvedLaserbeams_Solyn;

        float armOrientation = NPC.AngleTo(Target.Center) - PiOver2;
        float idealCannonAngle = NPC.AngleTo(Target.Center);
        RailgunCannonAngle = RailgunCannonAngle.AngleLerp(idealCannonAngle, 0.1f);
        EnergyCannonAngle = EnergyCannonAngle.AngleLerp(idealCannonAngle, 0.1f);

        // Move Mars' arms.
        float handMovementSpeed = InverseLerp(0f, 15f, AITimer);
        Vector2 leftArmOffset = new Vector2(-100f, 150f).RotatedBy(armOrientation);
        Vector2 rightArmOffset = new Vector2(100f, 150f).RotatedBy(armOrientation);
        if (leftArmOffset.X < 0f)
            leftArmOffset.X *= 1.4f;
        if (rightArmOffset.X > 0f)
            rightArmOffset.X *= 1.4f;

        MoveArmsTowards(leftArmOffset, rightArmOffset, handMovementSpeed * 0.25f, 0.7f);

        // Rotate based on horizontal speed.
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.016f, 0.4f);

        // Redirect above the target.
        if (AITimer <= CarvedLaserbeam_MarsRedirectTime)
        {
            Vector2 hoverDestination = Target.Center - Vector2.UnitY * 350f;
            NPC.SmoothFlyNear(hoverDestination, 0.1f, 0.9f);
            return;
        }

        // Give players infinite flight time.
        foreach (Player player in Main.ActivePlayers)
            CalamityCompatibility.GrantInfiniteCalFlight(player);

        // Slow down and charge energy.
        int relativeTimer = AITimer - CarvedLaserbeam_MarsRedirectTime;
        if (relativeTimer <= CarvedLaserbeam_LaserChargeUpTime)
        {
            // Play a charge-up sound.
            if (relativeTimer == 1)
                SoundEngine.PlaySound(GennedAssets.Sounds.Mars.ExoelectricDisintegrationRayChargeUp);

            float energyChargeUpCompletion = relativeTimer / (float)CarvedLaserbeam_LaserChargeUpTime;
            float energyChargeUpPower = InverseLerpBump(0f, 0.5f, 0.65f, 0.83f, energyChargeUpCompletion);
            int electricArcSpawnChance = (int)Lerp(16f, 4f, energyChargeUpPower);

            for (int i = 0; i < 12; i++)
            {
                if (Main.rand.NextBool(Sqrt(energyChargeUpPower) * 0.5f))
                {
                    Color streakColor = Color.Lerp(Color.Crimson, Color.Orange, Main.rand.NextFloat(0.65f));
                    Vector2 streakPosition = CorePosition + Main.rand.NextVector2Unit() * Main.rand.NextFloat(100f, energyChargeUpPower * 600f + 540f);
                    Vector2 streakVelocity = (CorePosition - streakPosition) * 0.085f;
                    Vector2 startingScale = new Vector2(0.4f, 0.012f);
                    Vector2 endingScale = new Vector2(0.9f, 0.004f);
                    if (Main.rand.NextBool(6))
                    {
                        streakColor = Color.Wheat;
                        startingScale.Y *= 1.5f;
                        endingScale.Y *= 1.6f;
                    }

                    LineStreakParticle energy = new LineStreakParticle(streakPosition, streakVelocity, streakColor, 11, streakVelocity.ToRotation(), startingScale, endingScale);
                    energy.Spawn();

                    if (Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool(electricArcSpawnChance))
                    {
                        float arcReachInterpolant = Main.rand.NextFloat();
                        int arcLifetime = Main.rand.Next(6, 14);
                        Vector2 arcSpawnPosition = streakPosition + Main.rand.NextVector2Circular(8f, 8f);
                        Vector2 arcOffset = streakPosition.SafeDirectionTo(CorePosition).RotatedByRandom(0.33f) * Lerp(150f, 650f, Pow(arcReachInterpolant, 1.9f));
                        NewProjectileBetter(NPC.GetSource_FromAI(), arcSpawnPosition, arcOffset, ModContent.ProjectileType<SmallTeslaArc>(), 0, 0f, -1, arcLifetime, 1f, Main.rand.NextFloat(1f, 2f));
                    }
                }

                if (Main.rand.NextBool(energyChargeUpPower * 0.1f))
                {
                    Color glowColor = Color.Lerp(Color.Crimson, Color.Orange, Main.rand.NextFloat(0.6f)) * 0.5f;
                    GlowyShardParticle glow = new GlowyShardParticle(CorePosition + Main.rand.NextVector2Circular(300f, 300f), Main.rand.NextVector2Circular(2f, 2f), Color.White, glowColor, 0.65f, 0.3f, 45);
                    glow.Spawn();
                }
            }

            ScreenShakeSystem.SetUniversalRumble(energyChargeUpPower.Squared() * 9f, TwoPi, null, 0.3f);
            NPC.velocity *= 0.93f;
            return;
        }

        relativeTimer -= CarvedLaserbeam_LaserChargeUpTime;

        // Fire the laser, shake the screen, and cause various visual effects.
        if (relativeTimer == 1)
        {
            // Play a laser firing sound.
            if (relativeTimer == 1)
            {
                SlotId fireSoundSlot = SoundEngine.PlaySound(GennedAssets.Sounds.Mars.ExoelectricDisintegrationRayFire);
                if (SoundEngine.TryGetActiveSound(fireSoundSlot, out ActiveSound? fireSound) && fireSound is not null)
                    fireSound.Volume *= 2.1f;
            }

            ScreenShakeSystem.StartShake(28f, TwoPi, null, 0.6f);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                CarvedLaserbeam_LaserbeamDirection = CorePosition.AngleTo(Target.Center);
                NPC.velocity -= NPC.SafeDirectionTo(Target.Center) * 32f;
                NPC.netUpdate = true;

                NewProjectileBetter(NPC.GetSource_FromAI(), CorePosition, Vector2.Zero, ModContent.ProjectileType<ExoelectricDisintegrationRay>(), ExoelectricDisintegrationRayDamage, 0f, -1, NPC.whoAmI);

                for (int i = 0; i < 6; i++)
                {
                    int fieldLifetime = SecondsToFrames(i * 0.15f + 0.4f);
                    NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center + NPC.velocity * i * 2f + Main.rand.NextVector2Circular(80f, 80f), Vector2.Zero, ModContent.ProjectileType<TeslaField>(), 0, 0f, -1, fieldLifetime, 6.5f);
                }
            }
        }

        // Lose HP as the laserbeam fires, staying immune to natural incoming damage.
        float attackCompletion = InverseLerp(0f, CarvedLaserbeam_LaserShootTime, relativeTimer);
        float idealLifeRatio = SmoothStep(Phase4LifeRatio, 0.01f, attackCompletion);
        if (NPC.life > NPC.lifeMax * idealLifeRatio)
            NPC.life = (int)(NPC.lifeMax * idealLifeRatio);
        NPC.dontTakeDamage = true;

        float whiteOverlayInterpolant = InverseLerp(0.785f, 0.95f, attackCompletion);
        if (whiteOverlayInterpolant >= 0.001f)
        {
            // Disable contact damage on the deathray as the white overlay takes over, since obviously the player won't be able to see the borders of it anymore.
            int rayID = ModContent.ProjectileType<ExoelectricDisintegrationRay>();
            var rays = AllProjectilesByID(rayID);
            foreach (Projectile ray in rays)
                ray.damage = 0;

            TotalScreenOverlaySystem.OverlayInterpolant = whiteOverlayInterpolant * 1.3f;
            TotalScreenOverlaySystem.OverlayColor = Color.White;
            ScreenShakeSystem.StartShake(whiteOverlayInterpolant.Squared() * 5f);
        }

        // Die when done attacking.
        if (relativeTimer >= CarvedLaserbeam_LaserShootTime)
        {
            Main.BestiaryTracker.Kills.RegisterKill(NPC);
            BossDownedSaveSystem.SetDefeatState<MarsBody>(true);

            SoundEngine.PlaySound(GennedAssets.Sounds.NPCKilled.MarsDeath).WithVolumeBoost(1.25f);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                NPC.NPCLoot();
                NPC.active = false;
                NPC.life = 0;
                NewProjectileBetter(NPC.GetSource_Death(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SyntheticSeedlingProjectile>(), 0, 0f);
            }
        }

        // Reorient the laser if the player manages to escape it, such as with a rod of discord.
        Vector2 perpendicularLaserDirection = (CarvedLaserbeam_LaserbeamDirection + PiOver2).ToRotationVector2();
        float horizontalDistanceFromLaser = Abs(SignedDistanceToLine(Target.Center, CorePosition, perpendicularLaserDirection));
        float idealDirection = CarvedLaserbeam_LaserbeamDirection;
        if (horizontalDistanceFromLaser >= CarvedLaserbeam_LaserWidth * 0.67f)
            idealDirection = CorePosition.AngleTo(Target.Center);

        // Use momentum with the laser redirect.
        float idealAngularVelocity = WrapAngle(idealDirection - CarvedLaserbeam_LaserbeamDirection) * InverseLerp(0.4f, 1.5f, horizontalDistanceFromLaser / CarvedLaserbeam_LaserWidth) * 0.08f;

        CarvedLaserbeam_LaserbeamAngularVelocity = Lerp(CarvedLaserbeam_LaserbeamAngularVelocity, idealAngularVelocity, 0.1f);
        CarvedLaserbeam_LaserbeamDirection += CarvedLaserbeam_LaserbeamAngularVelocity;

        // Slow down if in tiles.
        // Otherwise, try to move away from the target, slowly.
        if (Collision.SolidCollision(NPC.TopLeft, NPC.width, NPC.height))
            NPC.velocity *= 0.94f;
        else
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(Target.Center) * -7f, 0.2f);
    }

    /// <summary>
    /// Performs Solyn's part in Mars' carved laserbeam attack.
    /// </summary>
    public void DoBehavior_CarvedLaserbeams_Solyn(BattleSolyn solyn)
    {
        if (solyn.IsMultiplayerClone)
        {
            solyn.Invisible = true;
            return;
        }

        NPC solynNPC = solyn.NPC;
        solyn.UseStarFlyEffects();

        SolynStarAction = star => DoBehavior_CarvedLaserbeams_SolynStar(solyn, star);

        int postLaserShootTime = AITimer - CarvedLaserbeam_MarsRedirectTime - CarvedLaserbeam_LaserChargeUpTime;
        bool separatedFromStar = AnyProjectiles(ModContent.ProjectileType<SolynSentientStar>());
        if (postLaserShootTime <= 30)
        {
            Vector2 hoverDestination = solyn.Player.Center - Vector2.UnitX * solyn.Player.direction * 50f;

            if (postLaserShootTime >= -50)
            {
                solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(NPC.Center);
                hoverDestination += solyn.Player.SafeDirectionTo(NPC.Center) * 300f;
            }
            else
                solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(solyn.Player.Center);
            solynNPC.SmoothFlyNear(hoverDestination, 0.09f, 0.88f);

            // Make Solyn's star detach from Solyn, serving as an independent shield, alongside the magic forcefield.
            if (Main.netMode != NetmodeID.MultiplayerClient && postLaserShootTime == -30 && !separatedFromStar)
            {
                CarvedLaserbeam_LaserbeamDirection = CorePosition.AngleTo(solyn.Player.Center);
                NPC.netUpdate = true;

                NewProjectileBetter(solynNPC.GetSource_FromAI(), solynNPC.Center, CarvedLaserbeam_LaserbeamDirection.ToRotationVector2() * -9f, ModContent.ProjectileType<SolynSentientStar>(), 0, 0f);
                solynNPC.velocity *= 0.25f;
                solynNPC.netUpdate = true;
            }
        }
        else
        {
            float swerve = Sin(TwoPi * postLaserShootTime / 240f) * 0.3f;
            float flySpeed = Utils.Remap(solynNPC.Distance(NPC.Center), 240f, 85f, 7f, 0.1f);
            Vector2 hoverDestination = NPC.Center;
            Vector2 idealVelocity = solynNPC.SafeDirectionTo(hoverDestination).RotatedBy(swerve) * flySpeed;
            solynNPC.velocity = Vector2.Lerp(solynNPC.velocity, idealVelocity, 0.06f);
            solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(NPC.Center);
        }

        // Update Solyn's frames.
        if (separatedFromStar)
        {
            if (postLaserShootTime >= -30)
                solyn.Frame = 44f;
            if (postLaserShootTime >= 0)
                solyn.Frame = 45f;
        }

        // GLOW!!
        solyn.BackglowScale = InverseLerp(0f, 30f, AITimer) * 0.8f + 1f;

        if (Main.netMode != NetmodeID.MultiplayerClient && postLaserShootTime == -30)
            NewProjectileBetter(solynNPC.GetSource_FromAI(), solynNPC.Center, Vector2.Zero, ModContent.ProjectileType<DirectionalSolynForcefield2>(), 0, 0f);

        float idealRotation = solynNPC.velocity.X * 0.016f;
        if (postLaserShootTime >= 0)
        {
            idealRotation = CarvedLaserbeam_LaserbeamDirection;
            if (solynNPC.spriteDirection == 1)
                idealRotation += Pi;
        }

        solynNPC.rotation = solynNPC.rotation.AngleLerp(idealRotation, 0.4f);
    }

    /// <summary>
    /// Makes Solyn's star stay ahead of Solyn during Mars' carved laserbeams attack.
    /// </summary>
    public void DoBehavior_CarvedLaserbeams_SolynStar(BattleSolyn solyn, SolynSentientStar star)
    {
        if (StateMachine.CurrentState is null)
            return;

        Projectile starProjectile = star.Projectile;

        int idealFrame = 0;
        bool doneAttacking = AITimer >= CarvedLaserbeam_MarsRedirectTime + CarvedLaserbeam_LaserChargeUpTime + CarvedLaserbeam_LaserShootTime;
        if (doneAttacking)
        {
            if (starProjectile.frame == 0)
            {
                starProjectile.SmoothFlyNear(solyn.NPC.Center, 0.3f, 0.8f);
                if (starProjectile.WithinRange(solyn.NPC.Center, 20f))
                    starProjectile.Kill();
            }
        }
        else
        {
            starProjectile.SmoothFlyNear(solyn.NPC.Center - CarvedLaserbeam_LaserbeamDirection.ToRotationVector2() * 60f, 0.4f, 0.8f);
            idealFrame = 2;
        }

        if (star.Time % 12f == 11f)
            starProjectile.frame = Utils.Clamp(starProjectile.frame + Sign(idealFrame - starProjectile.frame), 0, 2);
        starProjectile.rotation = CarvedLaserbeam_LaserbeamDirection;
    }
}
