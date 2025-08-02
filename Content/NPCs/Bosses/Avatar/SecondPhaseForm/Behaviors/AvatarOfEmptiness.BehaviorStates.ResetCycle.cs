using Luminance.Common.StateMachines;
using Terraria;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// How many attack patterns the Avatar must go through in a cycle in order to perform a neutral attack pattern.<br></br>
    /// If its value is three, as an example, that means that every third pattern will be a distance-neutral one.
    /// </summary>
    public static int NeutralPatternCycleRate => 4;

    /// <summary>
    /// How far the player needs to be from the Avatar in order to perform his far-distanced patterns.
    /// </summary>
    public static float ProximityNeededForCloseAttacks => 450f;

    [AutomatedMethodInvoke]
    public void LoadState_ResetCycle()
    {
        StateMachine.RegisterTransition(AvatarAIType.ResetCycle, null, false, () => true, ResetCycle);
    }

    /// <summary>
    /// Resets the Avatar's attack cycle, choosing a new set of states for him to transition between.
    /// </summary>
    public void ResetCycle()
    {
        // Clear the state stack.
        StateMachine.StateStack.Clear();

        List<AvatarAIType> attackPattern = ChooseNewPattern();
        PreviousPattern = attackPattern;

        // Supply the state stack with the attack pattern.
        for (int i = attackPattern.Count - 1; i >= 0; i--)
            StateMachine.StateStack.Push(StateMachine.StateRegistry[attackPattern[i]]);

        // Increment the attack pattern counter.
        AttackPatternCounter++;

        NPC.netUpdate = true;
    }

    /// <summary>
    /// Chooses a new attack pattern for the Avatar to use based in attributes such as current phase and world state.
    /// </summary>
    public List<AvatarAIType> ChooseNewPattern()
    {
        if (Main.zenithWorld)
            return [AvatarAIType.GivePlayerHeadpats];

        if (Phase3)
        {
            List<AvatarAIType> overallPattern = GenerateDimensionPattern();
            if (!HasSentPlayersThroughVortex && overallPattern.Count >= 3)
                FirstDimensionAttack = overallPattern[2];
            return overallPattern;
        }

        // Decide which attack pattern to use.
        // Every few patterns a distance-neutral attack is performed. Otherwise, the Avatar will tailor has patterns to the player's position, resulting in a back-and-forth of
        // zoning where the Avatar forces the player into specific circumstances.
        int tries = 0;
        List<AvatarAIType> attackPattern;
        do
        {
            attackPattern = [];
            if (AttackPatternCounter % NeutralPatternCycleRate == NeutralPatternCycleRate - 1)
                attackPattern.AddRange(Main.rand.Next(Phase2Attacks_Neutral));
            else if (NPC.WithinRange(Target.Center, ProximityNeededForCloseAttacks + Main.rand.NextFloat(-60f, 60f)))
                attackPattern.AddRange(Main.rand.Next(Phase2Attacks_Close));
            else
                attackPattern.AddRange(Main.rand.Next(Phase2Attacks_Far));
            tries++;
        }
        while (ShouldRerollState(attackPattern, PreviousPattern, PreviousState) && tries < 200);

        return attackPattern;
    }

    /// <summary>
    /// Generates a randomly selected set of attacks that make all players travel throughout sections of the Avatar's universe.
    /// </summary>
    public List<AvatarAIType> GenerateDimensionPattern()
    {
        if (NeedsToSelectNewDimensionAttacksSoon)
        {
            List<AvatarAIType> set = Phase4StartingAttackSet.ToList();
            if (NeedsToSelectNewDimensionAttacksSoon)
                set.RemoveAt(0);
            if (NeedsToSelectNewDimensionAttacksSoon && set[0] != AvatarAIType.SendPlayerToMyUniverse && PreviousState != AvatarAIType.SendPlayerToMyUniverse)
            {
                set.Insert(0, AvatarAIType.SendPlayerToMyUniverse);
                NeedsToSelectNewDimensionAttacksSoon = false;
            }

            return set;
        }

        // Construct sets of attacks that occur for a designated dimension. This involves shuffling the above sets and then adding consistent final attacks at the end.
        List<AvatarAIType[]> cryonicFogAttacks = SelectableCryonicFogAttacks.ToList();
        List<AvatarAIType[]> cryonicAttacks = SelectableCryonicAttacks.ToList();
        cryonicAttacks.Add([AvatarAIType.AbsoluteZeroOutburst]);

        List<AvatarAIType[]> visceraAttacks = SelectableVisceraAttacks.OrderBy(a => Main.rand.NextFloat()).ToList();

        // Special case: Ensure that the bloodied fountain blasts is not the starting attack for the visceral dimension.
        // This ensures that the player doesn't start the dimension off with a state where the Avatar is invulnerable.
        if (visceraAttacks[0][0] == AvatarAIType.BloodiedFountainBlasts)
            visceraAttacks.Reverse();
        if (Phase4)
            visceraAttacks.Add([AvatarAIType.EnterBloodWhirlpool, AvatarAIType.AntishadowOnslaught]);

        List<AvatarAIType[]> darknessAttacks = SelectableDarknessAttacks.ToList();
        if (Phase4)
            darknessAttacks.Add([AvatarAIType.UniversalAnnihilation, AvatarAIType.TeleportAbovePlayer]);

        // Shuffle the set of dimension patterns at random, so that the player goes to different dimensions in different orders.
        // This process does not allow the same dimensions to occur twice in a row.
        List<AvatarAIType> previousAttacksLastToFirst = new List<AvatarAIType>();
        if (PreviousPattern is not null)
        {
            for (int i = PreviousPattern.Count - 1; i >= 0; i--)
                previousAttacksLastToFirst.Add(PreviousPattern[i]);
        }

        List<List<AvatarAIType[]>> dimensionPatterns = [cryonicAttacks, visceraAttacks, cryonicFogAttacks];

        for (int tries = 0; tries < 100; tries++)
        {
            dimensionPatterns = dimensionPatterns.OrderBy(a => Main.rand.NextFloat()).ToList();
            if (!previousAttacksLastToFirst.Take(7).ToList().Contains(dimensionPatterns[0][0][0]))
                break;
        }
        dimensionPatterns.Insert(2, darknessAttacks);

        // Construct the overall pattern, adding the send and return states before/after everything else.
        List<AvatarAIType> overallPattern = new List<AvatarAIType>(16);
        for (int i = 0; i < dimensionPatterns.Count; i++)
        {
            overallPattern.Add(AvatarAIType.SendPlayerToMyUniverse);

            // Special case: The first time the Avatar sends players to a place in his home universe, they need to first go through the vortex.
            if (!HasSentPlayersThroughVortex && i == 0)
                overallPattern.Add(AvatarAIType.TravelThroughVortex);

            foreach (AvatarAIType[] subPattern in dimensionPatterns[i])
                overallPattern.AddRange(subPattern);

            if (i < dimensionPatterns.Count - 1)
                overallPattern.Add(AvatarAIType.TeleportAbovePlayer);
        }

        return overallPattern;
    }

    /// <summary>
    /// Determines whether a given potential pattern of the Avatar AI states should be rerolled in favor of a different one based on past pattern information.
    /// </summary>
    /// <param name="potentialPattern">The potential pattern whose eligibility should be decided.</param>
    /// <param name="previousPattern">The previous pattern of attacks the Avatar performed.</param>
    /// <param name="previousState">The previous state the Avatar performed.</param>
    public static bool ShouldRerollState(List<AvatarAIType> potentialPattern, List<AvatarAIType> previousPattern, AvatarAIType previousState)
    {
        // Disallow repeats of the same attack.
        if (potentialPattern.FirstOrDefault(p => p != AvatarAIType.TeleportAbovePlayer) == previousState)
            return true;

        // Disallow repeats of the same pattern.
        if (potentialPattern.SequenceEqual(previousPattern ?? []))
            return true;

        return false;
    }
}
