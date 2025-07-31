using Luminance.Common.StateMachines;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    [AutomatedMethodInvoke]
    public void LoadStateTransitions_ResetCycle()
    {
        // Load the transition from IntroScreamAnimation to the typical cycle.
        StateMachine.RegisterTransition(NamelessAIType.ResetCycle, null, false, () => true,
            () =>
            {
                // Clear the state stack.
                StateMachine.StateStack.Clear();

                // Get the current attack cycle.
                List<NamelessAIType> phaseCycle = Phase1Cycle.ToList();
                if (CurrentPhase == 1)
                    phaseCycle = Phase2Cycle.ToList();
                if (CurrentPhase == 2)
                    phaseCycle = Phase3Cycle.ToList();

                if (TestOfResolveSystem.IsActive)
                {
                    phaseCycle.AddRange(Phase1Cycle);
                    if (DifficultyFactor >= 1.24f)
                        phaseCycle.AddRange(Phase2Cycle);
                    if (DifficultyFactor >= 1.4f)
                        phaseCycle.AddRange(Phase3Cycle);

                    // Remove certain attacks from the cycle.
                    while (phaseCycle.Remove(NamelessAIType.CrushStarIntoQuasar) ||
                           phaseCycle.Remove(NamelessAIType.DarknessWithLightSlashes) ||
                           phaseCycle.Remove(NamelessAIType.ConjureExplodingStars) ||
                           phaseCycle.Remove(NamelessAIType.PerpendicularPortalLaserbeams))
                    {

                    }

                    if (DifficultyFactor <= 1.35f)
                        phaseCycle.Add(NamelessAIType.PerpendicularPortalLaserbeams);

                    phaseCycle = phaseCycle.OrderByDescending(a => Main.rand.NextFloat()).Distinct().ToList();

                    for (int i = 0; i < phaseCycle.Count; i++)
                    {
                        if (phaseCycle[i] == NamelessAIType.SunBlenderBeams)
                        {
                            phaseCycle.Insert(i + 1, NamelessAIType.CrushStarIntoQuasar);
                            i++;
                        }
                    }
                }

                // Insert glock shooting after the sword attack in GFB.
                if (Main.zenithWorld)
                {
                    for (int i = 0; i < phaseCycle.Count; i++)
                    {
                        if (phaseCycle[i] == NamelessAIType.SwordConstellation)
                        {
                            phaseCycle.Insert(i + 1, NamelessAIType.Glock);
                            i++;
                        }
                    }
                }

                // Supply the state stack with the attack cycle.
                for (int i = phaseCycle.Count - 1; i >= 0; i--)
                    StateMachine.StateStack.Push(StateMachine.StateRegistry[phaseCycle[i]]);
            });
    }
}
