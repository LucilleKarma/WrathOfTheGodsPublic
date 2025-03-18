using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.CrossCompatibility.Inbound.BossChecklist;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC, IBossChecklistSupport
{
    private PushdownAutomata<EntityAIState<NamelessAIType>, NamelessAIType> stateMachine;

    public PushdownAutomata<EntityAIState<NamelessAIType>, NamelessAIType> StateMachine
    {
        get
        {
            if (stateMachine is null)
                LoadStates();
            return stateMachine!;
        }
        set => stateMachine = value;
    }

    public ref int AITimer => ref StateMachine.CurrentState.Time;

    public void LoadStates()
    {
        // Initialize the AI state machine.
        StateMachine = new(new(NamelessAIType.Awaken));
        StateMachine.OnStateTransition += ResetGenericVariables;

        // Register all nameless deity states in the machine.
        for (int i = 0; i < (int)NamelessAIType.Count; i++)
            StateMachine.RegisterState(new((NamelessAIType)i));

        // Load state transitions.
        AutomatedMethodInvokeAttribute.InvokeWithAttribute(this);
    }

    public void ResetGenericVariables(bool stateWasPopped, EntityAIState<NamelessAIType> oldState)
    {
        GeneralHoverOffset = Vector2.Zero;
        NPC.Opacity = 1f;

        // Reset generic AI variables if the state was popped.
        // If it wasn't popped, these should be retained for the purpose of preserving AI information when going back to the previous state.
        if (stateWasPopped)
        {
            NPC.ai[2] = NPC.ai[3] = 0f;
        }

        // Reset hands.
        for (int i = 0; i < Hands.Count; i++)
        {
            NamelessDeityHand hand = Hands[i];
            hand.HasArms = true;
            hand.HandType = NamelessDeityHandType.Standard;
            hand.ArmInverseKinematicsFlipOverride = null;
        }

        // Reset things when preparing for entering the new phase.
        if (CurrentState == NamelessAIType.EnterPhase2)
        {
            CurrentPhase = 1;
            WaitingForPhase2Transition = false;
            ClearAllProjectiles();
        }
        if (CurrentState == NamelessAIType.EnterPhase3)
        {
            CurrentPhase = 2;
            ClearAllProjectiles();
        }

        // Mark the Moment of Creation attack as witnessed if it was just selected.
        if (CurrentState == NamelessAIType.MomentOfCreation)
            HasExperiencedFinalAttack = true;

        // Reset texture variants if Nameless isn't visible.
        if (NPC.Opacity <= 0.01f || TeleportVisualsAdjustedScale.Length() <= 0.05f || !NPC.WithinRange(Target.Center, 1100f))
            RerollAllSwappableTextures();

        if (oldState is null || oldState.Identifier != NamelessAIType.SunBlenderBeams)
            DestroyAllHands();
        TargetClosest();
        NPC.netUpdate = true;

        if (TestOfResolveSystem.IsActive && (oldState is null || oldState.Identifier != NamelessAIType.Teleport))
        {
            // Recalculate Nameless' difficulty factor.
            // This should gradually increase further and further as time passes.
            // Minute 0: Base difficulty starts at 1. Self-explanatory.
            // Minute 1: Base difficulty gradually climbs to 1.25. Similar to the base fight, but with a slight boost. After this, the phase 2 attacks can occur.
            // Minute 2: Base difficulty gradually climbs to 1.55. Similar to the base fight, but with a slight boost. Shortly before this, the phase 3 attacks can occur, unlocking everything.
            // Minute 3.5: Base difficulty gradually reaches 2.3. The attacks begin ramping up.
            // Minute 6: Base difficulty reaches 3.5. The attacks are now extremely difficult.
            // Minute 6 and onwards: The difficulty continues to slowly climb until the player dies.
            float minutesPassed = FightTimer / 3600f;
            DifficultyFactor = 1f;
            DifficultyFactor += InverseLerp(0f, 1f, minutesPassed) * 0.25f;
            DifficultyFactor += InverseLerp(1f, 2f, minutesPassed) * 0.3f;
            DifficultyFactor += InverseLerp(2f, 3.5f, minutesPassed) * 0.75f;
            DifficultyFactor += InverseLerp(3.5f, 6f, minutesPassed) * 1.2f;
            if (minutesPassed > 6f)
                DifficultyFactor += Pow(minutesPassed - 6f, 0.71f);

            // Relax the difficulty factor.
            DifficultyFactor = Pow(DifficultyFactor, 0.27f);
            if (minutesPassed > 12f)
                DifficultyFactor += (minutesPassed - 12f) * 0.174f;
        }
    }

    public void ForceNextAttack(NamelessAIType state)
    {
        if (IsAttackState(CurrentState))
            StateMachine.StateStack.Push(StateMachine.StateRegistry[state]);
    }

    public static bool IsAttackState(NamelessAIType state) => Phase1Cycle.Contains(state) || Phase2Cycle.Contains(state) || Phase3Cycle.Contains(state);

    public static void ApplyToAllStatesWithCondition(Action<NamelessAIType> action, Func<NamelessAIType, bool> condition)
    {
        for (int i = 0; i < (int)NamelessAIType.Count; i++)
        {
            NamelessAIType attack = (NamelessAIType)i;
            if (!condition(attack))
                continue;

            action(attack);
        }
    }
}
