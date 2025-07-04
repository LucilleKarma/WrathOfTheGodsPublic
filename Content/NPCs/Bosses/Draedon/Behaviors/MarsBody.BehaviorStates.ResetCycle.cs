using Luminance.Common.DataStructures;
using Luminance.Common.StateMachines;
using NoxusBoss.Content.NPCs.Friendly;
using Terraria;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    [AutomatedMethodInvoke]
    public void LoadState_ResetCycle()
    {
        StateMachine.RegisterTransition(MarsAIType.ResetCycle, null, false, () => true, ResetCycle);
    }

    /// <summary>
    /// Resets Mars' attack cycle, choosing a new set of states for him to transition between.
    /// </summary>
    public void ResetCycle()
    {
        // Clear the state stack.
        StateMachine.StateStack.Clear();

        // Clear Solyn's projectiles.
        IProjOwnedByBoss<BattleSolyn>.KillAll();

        List<MarsAIType> attackPattern = ChooseNewPattern();

        // Supply the state stack with the attack pattern.
        for (int i = attackPattern.Count - 1; i >= 0; i--)
            StateMachine.StateStack.Push(StateMachine.StateRegistry[attackPattern[i]]);

        PreviousState = attackPattern.Last();

        ResetSolynPlayerTeamAttackTimers();

        NPC.netUpdate = true;
    }

    /// <summary>
    /// Chooses a new attack pattern for Mars to use.
    /// </summary>
    public List<MarsAIType> ChooseNewPattern()
    {
        float lifeRatio = Saturate(NPC.life / (float)NPC.lifeMax);
        if (lifeRatio >= Phase2LifeRatio)
            return [MarsAIType.FirstPhaseMissileRailgunCombo];

        if (lifeRatio >= Phase3LifeRatio)
            return [MarsAIType.EnergyWeaveSequence];

        if (lifeRatio >= Phase4LifeRatio)
        {
            List<MarsAIType> potentialStates = [MarsAIType.UpgradedMissileRailgunCombo, MarsAIType.Malfunction, MarsAIType.BrutalBarrage];
            potentialStates.Remove(PreviousState);

            return [Main.rand.Next(potentialStates)];
        }

        return [MarsAIType.CarvedLaserbeam];
    }
}
