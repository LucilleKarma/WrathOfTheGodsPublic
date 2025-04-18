using Luminance.Core.Graphics;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.Graphics.UI.SolynDialogue;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Friendly;

public partial class Solyn : ModNPC, IPixelatedPrimitiveRenderer
{
    /// <summary>
    /// Whether Solyn can be spoken to.
    /// </summary>
    public bool CanBeSpokenTo
    {
        get;
        set;
    }

    /// <summary>
    /// Whether a forced conversation has been initiated.
    /// </summary>
    public bool ForcedConversation
    {
        get;
        set;
    }

    /// <summary>
    /// The current conversation Solyn is using.
    /// </summary>
    public Conversation CurrentConversation
    {
        get;
        set;
    }

    public void DoBehavior_SpeakToPlayer()
    {
        ReelInKite = false;
        NPC.velocity.X *= 0.7f;
        NPC.rotation = 0f;
        PerformStandardFraming();

        if (DialogueManager.FindByRelativePrefix("SolynIntroduction").SeenBefore("Question1") || CurrentConversation != DialogueManager.FindByRelativePrefix("SolynIntroduction"))
            NPC.spriteDirection = (Main.LocalPlayer.Center.X - NPC.Center.X).NonZeroSign();

        SolynDialogUIManager ui = ModContent.GetInstance<SolynDialogSystem>().DialogUI;
        if (AITimer % 18 >= 9 && ui.DialogueText != ui.ResponseToSay && ui.NextCharacterDelay <= 10)
            Frame = 19;

        if (!((Main.LocalPlayer.talkNPC == NPC.whoAmI && CanBeSpokenTo) || ForcedConversation))
            SwitchState(SolynAIType.StandStill);
    }
}
