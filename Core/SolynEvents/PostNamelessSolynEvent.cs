using Luminance.Core.Cutscenes;
using Microsoft.Xna.Framework;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.Graphics.UI.Books;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using NoxusBoss.Core.World.Subworlds;
using SubworldLibrary;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.SolynEvents;

public class PostNamelessSolynEvent : SolynEvent
{
    public override int TotalStages => 1;

    public override void OnModLoad()
    {
        Conversation conversation = DialogueManager.RegisterNew("PostNamelessDiscussion", "Apology1").
            WithAppearanceCondition(instance => SubworldSystem.IsActive<EternalGardenNew>()).
            MakeSpokenByPlayer("ApologyResponse", "HowDidYouEndUpHereQuestion", "WhyDidYouSaveMeQuestion", "WhatNowQuestion", "DeityQuestion", "TreeQuestion").
            WithRootSelectionFunction(instance =>
            {
                if (instance.SeenBefore("Apology6"))
                    return instance.GetByRelativeKey("QuestionsStart");

                return instance.GetByRelativeKey("Apology1");
            });

        conversation.LinkChain("Apology1", "Apology2", "Apology3", "Apology4", "Apology5", "Apology6", "ApologyResponse", "QuestionsStart");
        conversation.LinkChain("QuestionsStart", "HowDidYouEndUpHereQuestion", "HowDidYouEndUpHere1", "HowDidYouEndUpHere2", "HowDidYouEndUpHere3", "HowDidYouEndUpHere4", "HowDidYouEndUpHere5", "QuestionsStart");
        conversation.LinkChain("QuestionsStart", "WhyDidYouSaveMeQuestion", "WhyDidYouSaveMe1", "WhyDidYouSaveMe2", "WhyDidYouSaveMe3", "WhyDidYouSaveMe4", "WhyDidYouSaveMe5", "QuestionsStart");
        conversation.LinkChain("QuestionsStart", "WhatNowQuestion", "WhatNow1", "WhatNow2", "WhatNow3", "WhatNow4", "WhatNow5", "QuestionsStart");
        conversation.LinkChain("QuestionsStart", "DeityQuestion", "Deity1", "Deity2", "Deity3", "QuestionsStart");
        conversation.LinkChain("QuestionsStart", "TreeQuestion", "Tree1", "Tree2");

        Dialogue treeQuestion = conversation.GetByRelativeKey("TreeQuestion");
        treeQuestion.ColorOverrideFunction = () => Color.Lerp(new(40, 175, 65), new(255, 251, 81), Cos01(Main.GlobalTimeWrappedHourly * 0.9f));
        treeQuestion.SelectionCondition = () => SolynBookExchangeRegistry.RedeemedAllBooks;
        conversation.GetByRelativeKey("Tree2").ClickAction = _ => CutsceneManager.QueueCutscene(ModContent.GetInstance<EndCreditsScene>());

        ConversationSelector.PriorityConversationSelectionEvent += SelectGardenDialogue;
    }

    private Conversation? SelectGardenDialogue()
    {
        if (!SubworldSystem.IsActive<EternalGardenNew>())
            return null;

        return DialogueManager.FindByRelativePrefix("PostNamelessDiscussion");
    }
}
