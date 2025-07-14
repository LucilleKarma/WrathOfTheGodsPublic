using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Physics.VerletIntergration;
using Terraria;
using Terraria.Audio;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    public static int SpawnAnimation_FallCountdownTime => 105;

    public static int SpawnAnimation_FlexDelay => 150;

    [AutomatedMethodInvoke]
    public void LoadState_SpawnAnimation()
    {
        StateMachine.RegisterTransition(MarsAIType.SpawnAnimation, MarsAIType.TeachPlayerAboutTeamAttack, false, () =>
        {
            return AITimer >= SpawnAnimation_FlexDelay + 120;
        });
        StateMachine.RegisterStateBehavior(MarsAIType.SpawnAnimation, DoBehavior_SpawnAnimation);
    }

    /// <summary>
    /// Performs Mars' spawn animation state, making him appear with attached wires before autonomously activating.
    /// </summary>
    public void DoBehavior_SpawnAnimation()
    {
        SolynAction = DoBehavior_SpawnAnimation_Solyn;

        int flexTime = 26;
        int flexTimer = AITimer - SpawnAnimation_FlexDelay;
        float flexInterpolant = InverseLerp(0f, flexTime, flexTimer);
        ulong seed = (ulong)(NPC.whoAmI + 237);
        for (int i = 0; i < Wires.Length; i++)
        {
            AttachedRope wire = Wires[i];
            float startDistanceFromCenter = wire.Rope.Rope[0].Position.Distance(NPC.Center);
            float upwardForce = startDistanceFromCenter * 0.4f + 5f;

            // Make Mars' wires attach to his arms.
            if (i < Wires.Length / 2)
                wire.StartingOffset = Vector2.Lerp(leftElbowPosition, LeftShoulderPosition, InverseLerp(0f, Wires.Length / 2, i));
            else
                wire.StartingOffset = Vector2.Lerp(rightElbowPosition, RightShoulderPosition, InverseLerp(Wires.Length / 2 + 1f, Wires.Length - 1f, i));
            wire.StartingOffset -= NPC.Center;

            // Apply a random offset to Mars' wires. This does not apply to wires at the end points.
            if (i != 0 && i != Wires.Length - 1)
                wire.StartingOffset += Vector2.UnitX * Lerp(-12f, 12f, Utils.RandomFloat(ref seed));

            wire.StartingOffset = wire.StartingOffset.RotatedBy(-NPC.rotation) / NPC.scale;
            wire.Update(NPC, -upwardForce, flexInterpolant <= 0.95f);

            // Make the strings disintegrate.
            if (flexTimer == (int)(flexTime * 0.95f))
            {
                float stringLength = 30f;
                float overallWireLength = wire.Rope.RopeLength;
                for (float dx = stringLength * 0.5f; dx < overallWireLength; dx += stringLength)
                {
                    if (!Main.rand.NextBool(3))
                        continue;

                    Vector2 stringPosition = NPC.Center + Vector2.Lerp(wire.StartingOffset, wire.EndingOffset, dx / overallWireLength).RotatedBy(NPC.rotation) * NPC.scale;
                    StringLineParticle stringParticle = new StringLineParticle(stringPosition, Main.rand.NextVector2Circular(30f, 25f) + Vector2.UnitY * 13f, WireColor, new Vector2(2f, stringLength), 0f, Main.rand.Next(60, 180));
                    stringParticle.Spawn();
                }
            }

            for (int j = 0; j < wire.Rope.Rope.Count; j++)
            {
                VerletSimulatedSegment segment = wire.Rope.Rope[j];
                {
                    float idealX = (NPC.Center + wire.StartingOffset.RotatedBy(NPC.rotation) * NPC.scale).X;
                    segment.Position.X = Lerp(segment.Position.X, idealX, 0.05f);
                }
            }
        }

        // Make Mars' lights flicker before flexing.
        float flickerInterpolant = Pow(InverseLerp(-40f, 0f, flexTimer), 1.6f);
        float flickerOpacity = (1f - Cos(flickerInterpolant * Pi * 5f)) * 0.5f;
        GlowmaskColor = Color.White * flickerOpacity.Squared();

        int flickerTimer = flexTimer;
        if (flickerTimer == -39)
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.LightFlickerOn with { MaxInstances = 0 }, NPC.Center);
        if (flickerTimer == -16)
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.LightFlickerOff with { MaxInstances = 0 }, NPC.Center);
        if (flickerTimer == -2)
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.LightFlickerOff with { MaxInstances = 0 }, NPC.Center);

        // Make Mars' thrusters activate after starting his flex.
        ThrusterStrength = InverseLerp(16f, 54f, flexTimer);

        // Update the arms.
        if (AITimer <= 5)
        {
            IdealLeftHandPosition = NPC.Center + new Vector2(-100f, 350f);
            IdealRightHandPosition = NPC.Center + new Vector2(100f, 350f);
            NPC.rotation = 0f;
        }
        if (LeftHandVelocity.Y < 20f)
            LeftHandVelocity += Vector2.UnitY * 0.3f;
        if (RightHandVelocity.Y < 20f)
            RightHandVelocity += Vector2.UnitY * 0.3f;

        RailgunCannonAngle = LeftShoulderPosition.AngleTo(LeftHandPosition);
        EnergyCannonAngle = RightShoulderPosition.AngleTo(RightHandPosition);

        // Descend from above at first.
        if (AITimer < SpawnAnimation_FallCountdownTime)
        {
            if (AITimer == 1)
                SoundEngine.PlaySound(GennedAssets.Sounds.Mars.Descend, NPC.Center);
            if (AITimer == SpawnAnimation_FallCountdownTime - 1)
                SoundEngine.PlaySound(GennedAssets.Sounds.Mars.PowerUp);

            float lowerInterpolant = AITimer / (float)SpawnAnimation_FallCountdownTime;
            NPC.SmoothFlyNear(Target.Center - Vector2.UnitY * SmoothStep(900f, 285f, lowerInterpolant), 0.3f, 0.7f);
        }
        else
            NPC.velocity *= 0.9f;

        // Make Mars flex when ready.
        if (flexInterpolant > 0f)
        {
            MoveArmsTowards(new Vector2(-112f, -126f), new Vector2(450f, -160f), 0.26f, SmoothStep(1f, 0.5f, flexInterpolant));

            if (flexTimer == (int)(flexTime * 0.66f))
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.Mars.CableSnap);
                SoundEngine.PlaySound(GennedAssets.Sounds.Mars.Laugh);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 25f, shakeStrengthDissipationIncrement: 0.45f);

                ExpandingChromaticBurstParticle burst = new ExpandingChromaticBurstParticle(NPC.Center, Vector2.Zero, Color.White, 40, 0.5f);
                burst.Spawn();
            }
            RailgunCannonAngle += flexInterpolant * PiOver2 * 0.7f;
        }

        // Otherwise, if not ready, simply keep Mars' hands to his sides.
        else
        {
            IdealLeftHandPosition = new(NPC.Center.X - 100f, IdealLeftHandPosition.Y);
            IdealRightHandPosition = new(NPC.Center.X + 100f, IdealRightHandPosition.Y);
        }

        NPC.rotation = (NPC.position.X - NPC.oldPosition.X) * 0.005f;
    }

    /// <summary>
    /// Instructs Solyn to stay near the player for Mars' spawn animation.
    /// </summary>
    public void DoBehavior_SpawnAnimation_Solyn(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;
        Vector2 lookDestination = solyn.Player.Center;
        Vector2 hoverDestination = solyn.Player.Center - Vector2.UnitX * solyn.Player.direction * 56f;

        solynNPC.SmoothFlyNear(hoverDestination, 0.2f, 0.8f);

        solyn.UseStarFlyEffects();
        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(lookDestination);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(0f, 0.3f);
    }
}
