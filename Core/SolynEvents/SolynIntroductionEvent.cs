using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.World.Subworlds;
using SubworldLibrary;

namespace NoxusBoss.Core.SolynEvents;

public class SolynIntroductionEvent : SolynEvent
{
    public override int TotalStages => 1;

    public override void OnModLoad()
    {
        DialogueManager.RegisterNew("SolynIntroduction", "Start").
            LinkFromStartToFinishExcluding("Repeat").
            WithAppearanceCondition(instance => !instance.Tree.HasBeenSeenBefore).
            MakeSpokenByPlayer("Question1", "Question2", "Question3").
            WithRerollCondition(_ => Finished).
            WithRootSelectionFunction(instance =>
            {
                if (instance.SeenBefore("Talk8"))
                    return instance.GetByRelativeKey("Repeat");

                return instance.GetByRelativeKey("Start");
            });

        ConversationSelector.PriorityConversationSelectionEvent += SelectIntroductionDialogue;
    }

    private Conversation? SelectIntroductionDialogue()
    {
        if (SubworldSystem.IsActive<EternalGardenNew>())
            return null;

        if (!Finished)
            return DialogueManager.FindByRelativePrefix("SolynIntroduction");

        return null;
    }

    public override void PostUpdateNPCs()
    {
        // Register this event as completed once Solyn has existed for long enough.
        if (Solyn is not null && Solyn.TimeSinceLastTalk >= 900 && Stage <= 0)
            SafeSetStage(1);
    }
}
