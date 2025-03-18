using Microsoft.Xna.Framework;
using NoxusBoss.Content.Items.Placeable;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace NoxusBoss.Core.SolynEvents;

public class CampSetupEvent : SolynEvent
{
    public override int TotalStages => 1;

    public static bool CanStart => !SolynCampsiteWorldGen.CampHasBeenMade && ModContent.GetInstance<SolynIntroductionEvent>().Finished;

    public override void OnModLoad()
    {
        DialogueManager.RegisterNew("SolynTentSetUpDialogue", "Start").
            LinkFromStartToFinish().
            WithRootSelectionFunction(conversation =>
            {
                if (conversation.SeenBefore("Solyn3"))
                    return conversation.GetByRelativeKey("Solyn3");

                return conversation.GetByRelativeKey("Start");
            }).
            WithAppearanceCondition(instance => CanStart && !Finished).
            WithRerollCondition(instance => !instance.AppearanceCondition()).
            MakeSpokenByPlayer("Player1").
            WithRerollCondition(_ => Finished);

        DialogueManager.FindByRelativePrefix("SolynTentSetUpDialogue").GetByRelativeKey("Solyn3").ClickAction = seenBefore =>
        {
            if (!seenBefore)
                Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), ModContent.ItemType<StrangeFlagpole>());
        };

        ConversationSelector.PriorityConversationSelectionEvent += SelectIntroductionDialogue;
    }

    private Conversation? SelectIntroductionDialogue()
    {
        if (!Finished && CanStart)
            return DialogueManager.FindByRelativePrefix("SolynTentSetUpDialogue");

        return null;
    }

    public override void PostUpdateNPCs()
    {
        bool done = DialogueManager.FindByRelativePrefix("SolynTentSetUpDialogue").SeenBefore("Solyn3") && SolynCampsiteWorldGen.CampHasBeenMade;
        if (WorldVersionSystem.WorldVersion < new Version(1, 2, 18) && SolynCampsiteWorldGen.CampSitePosition != Vector2.Zero)
            done = true;

        if (!Finished && done)
            SafeSetStage(1);
    }
}
