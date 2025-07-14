using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    [AutomatedMethodInvoke]
    public void LoadState_VulnerableUntilDeath()
    {
        StateMachine.RegisterStateBehavior(MarsAIType.VulnerableUntilDeath, DoBehavior_VulnerableUntilDeath);
        TeamBeamHitEffectEvent += OnHitByTeamBeam_VulnerableUntilDeath;
    }

    private void OnHitByTeamBeam_VulnerableUntilDeath(Projectile beam)
    {
        if (CurrentState != MarsAIType.VulnerableUntilDeath)
            return;

        var caster = Main.player[beam.owner];
        NPC.velocity += caster.SafeDirectionTo(NPC.Center) * beam.localNPCHitCooldown * 0.93f;
        NPC.netUpdate = true;
    }

    /// <summary>
    /// Performs Mars' VulnerableUntilDeath effect.
    /// </summary>
    public void DoBehavior_VulnerableUntilDeath()
    {
        SolynAction = DoBehavior_VulnerableUntilDeath_Solyn;

        float slumpRotation = Lerp(0.04f, 0.15f, EasingCurves.Cubic.Evaluate(EasingType.InOut, Cos01(TwoPi * AITimer / 155f)));
        float hitWobble = Sin01(NPC.velocity.X * 0.22f + AITimer / 9f) * NPC.velocity.X.NonZeroSign() * InverseLerp(5f, 10f, NPC.velocity.Length()) * 0.9f;
        MoveArmsTowards(new Vector2(-100f, 400f).RotatedBy(-slumpRotation), new Vector2(100f, 400f).RotatedBy(-slumpRotation));

        NPC.velocity *= 0.82f;
        NPC.velocity += Main.rand.NextVector2Unit() * 0.15f;

        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f + slumpRotation + hitWobble, 0.08f);
        RailgunCannonAngle = -NPC.rotation + PiOver2;

        // Emit electricity.
        if (Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool(3))
        {
            float arcReachInterpolant = Main.rand.NextFloat();
            int arcLifetime = Main.rand.Next(10, 16);
            Vector2 energySpawnPosition = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(76f, 120f) * NPC.scale;
            Vector2 arcOffset = NPC.SafeDirectionTo(energySpawnPosition).RotatedByRandom(0.75f) * Lerp(60f, 285f, Pow(arcReachInterpolant, 5f));
            NewProjectileBetter(NPC.GetSource_FromAI(), energySpawnPosition, arcOffset, ModContent.ProjectileType<SmallTeslaArc>(), 0, 0f, -1, arcLifetime);

            for (int i = 0; i < 3; i++)
            {
                Dust energyDust = Dust.NewDustPerfect(energySpawnPosition, DustID.Vortex);
                energyDust.scale *= Main.rand.NextFloat(1f, 1.5f);
                energyDust.noGravity = true;
            }
        }

        // Emit smoke.
        if (Main.rand.NextBool())
        {
            Vector2 smokeSpawnPosition = NPC.Center - Vector2.UnitY.RotatedByRandom(0.9f) * NPC.scale * Main.rand.NextFloat(60f, 90f);
            Vector2 smokeVelocity = -Vector2.UnitY.RotatedByRandom(0.71f) * Main.rand.NextFloat(10f);
            MediumSmokeParticle smoke = new MediumSmokeParticle(smokeSpawnPosition, smokeVelocity, Color.DarkSlateGray, 60, 0.8f, 0.1f);
            smoke.Spawn();
        }

        float flickerWave = AperiodicSin(TwoPi * AITimer / 40f);
        float flicker = Pow(flickerWave * 0.5f + 0.5f, 2.3f) * 1.3f;
        GlowmaskColor = Color.White * flicker;
    }

    /// <summary>
    /// Instructs Solyn to stay near the player for Mars' vulnerability effect.
    /// </summary>
    public void DoBehavior_VulnerableUntilDeath_Solyn(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;
        Vector2 lookDestination = solyn.Player.Center;
        Vector2 hoverDestination = solyn.Player.Center + new Vector2(solyn.Player.direction * -30f, -50f);

        solynNPC.Center = Vector2.Lerp(solynNPC.Center, hoverDestination, 0.033f);
        solynNPC.SmoothFlyNear(hoverDestination, 0.27f, 0.6f);

        solyn.UseStarFlyEffects();
        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(lookDestination);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(0f, 0.3f);

        HandleSolynPlayerTeamAttack(solyn);
    }
}
