using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.World.GameScenes.AvatarAppearances;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using Terraria.ModLoader;

namespace NoxusBoss.Core.SolynEvents;

public class GenesisIntroductionEvent : SolynEvent
{
    public override int TotalStages => 1;

    public static bool CanStart => ModContent.GetInstance<PermafrostKeepEvent>().Finished && (RiftEclipseManagementSystem.RiftEclipseOngoing || ModContent.GetInstance<PostMLRiftAppearanceSystem>().Ongoing);

    public override void OnModLoad()
    {
        DialogueManager.RegisterNew("GenesisRevealDiscussion", "Start").
            LinkFromStartToFinish().
            WithAppearanceCondition(instance => CanStart).
            WithRerollCondition(instance => !instance.AppearanceCondition()).
            MakeSpokenByPlayer("Player1", "Player2").
            WithRerollCondition(_ => Finished);

        ConversationSelector.PriorityConversationSelectionEvent += SelectIntroductionDialogue;
    }

    private Conversation? SelectIntroductionDialogue()
    {
        if (!Finished && CanStart)
            return DialogueManager.FindByRelativePrefix("GenesisRevealDiscussion");

        return null;
    }

    public override void PostUpdateNPCs()
    {
        if (!Finished && DialogueManager.FindByRelativePrefix("GenesisRevealDiscussion").SeenBefore("Solyn9") && CanStart)
            SafeSetStage(1);
    }
}
