using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Friendly;
using Terraria;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    [AutomatedMethodInvoke]
    public void LoadState_PostEnergyWeaveSequenceStun()
    {
        StateMachine.RegisterTransition(MarsAIType.PostEnergyWeaveSequenceStun, MarsAIType.ElectricCageBlasts, false, () =>
        {
            return NPC.life < NPC.lifeMax * Phase3LifeRatio;
        });
        StateMachine.RegisterStateBehavior(MarsAIType.PostEnergyWeaveSequenceStun, DoBehavior_PostEnergyWeaveSequenceStun);

        TeamBeamHitEffectEvent += OnHitByTeamBeam_PostEnergyWeaveSequenceStun;
    }

    private void OnHitByTeamBeam_PostEnergyWeaveSequenceStun(Projectile beam)
    {
        if (CurrentState != MarsAIType.PostEnergyWeaveSequenceStun)
            return;

        var caster = Main.player[beam.owner];
        float antiSpacePush = InverseLerp(900f, 2700f, NPC.Center.Y);
        NPC.velocity += caster.SafeDirectionTo(NPC.Center) * beam.localNPCHitCooldown * antiSpacePush * 0.3f;
        NPC.netUpdate = true;
    }

    /// <summary>
    /// Performs Mars' post-energy-weave-sequence stun effect, leaving him vulnerable to attack until he reaches a certain HP threshold.
    /// </summary>
    public void DoBehavior_PostEnergyWeaveSequenceStun()
    {
        SolynAction = DoBehavior_PostEnergyWeaveSequenceStun_Solyn;

        float slumpRotation = Lerp(0.05f, 0.34f, EasingCurves.Cubic.Evaluate(EasingType.InOut, Cos01(TwoPi * AITimer / 155f)));
        float hitWobble = Sin01(NPC.velocity.X * 0.22f + AITimer / 9f) * NPC.velocity.X.NonZeroSign() * InverseLerp(5f, 10f, NPC.velocity.Length()) * 0.9f;
        MoveArmsTowards(new Vector2(-100f, 400f).RotatedBy(-slumpRotation), new Vector2(100f, 400f).RotatedBy(-slumpRotation));

        NPC.velocity *= 0.945f;

        if (AITimer == 1 && !NPC.WithinRange(Target.Center, 1360f))
        {
            NPC.Center = Target.Center - Target.SafeDirectionTo(NPC.Center) * 1000f;
            NPC.velocity = NPC.SafeDirectionTo(Target.Center) * 37f;
            NPC.netUpdate = true;
        }

        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f + slumpRotation + hitWobble, 0.08f);
    }

    /// <summary>
    /// Instructs Solyn to stay near the player for the stun.
    /// </summary>
    public void DoBehavior_PostEnergyWeaveSequenceStun_Solyn(BattleSolyn solyn)
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
