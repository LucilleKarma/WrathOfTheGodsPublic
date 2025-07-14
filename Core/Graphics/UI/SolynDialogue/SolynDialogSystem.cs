using Microsoft.Xna.Framework;

using NoxusBoss.Content.NPCs.Friendly;

using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace NoxusBoss.Core.Graphics.UI.SolynDialogue;

public class SolynDialogSystem : ModSystem
{
    /// <summary>
    /// The UI responsible for drawing dialogue and player responses to said dialogue.
    /// </summary>
    public SolynDialogUIManager DialogUI
    {
        get;
        internal set;
    } = new SolynDialogUIManager();

    /// <summary>
    /// Whether the UI this system is responsible for is visible or not.
    /// </summary>
    public static bool Visible
    {
        get;
        private set;
    }

    public override void OnWorldLoad() => Visible = false;

    public override void OnWorldUnload() => Visible = false;

    public override void UpdateUI(GameTime gameTime)
    {
        if (Visible && !NPC.AnyNPCs(ModContent.NPCType<Solyn>()))
            HideUI();

        if (Visible)
            DialogUI.Update();
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        // Draw the Solyn dialogue UI.
        int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text", StringComparison.Ordinal));
        if (mouseTextIndex != -1)
        {
            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer("Wrath of the Gods: Solyn Dialogue", () =>
            {
                if (Visible)
                    DialogUI.Render();
                return true;
            }, InterfaceScaleType.None));
        }
    }

    /// <summary>
    /// Shows the dialogue UI.
    /// </summary>
    public static void ShowUI()
    {
        if (Visible)
            return;

        SolynDialogSystem system = ModContent.GetInstance<SolynDialogSystem>();
        system.DialogUI.DialogueText = string.Empty;
        system.DialogUI.ResetDialogueData();
        Visible = true;
    }

    /// <summary>
    /// Hides the dialogue UI.
    /// </summary>
    public static void HideUI()
    {
        if (!Visible)
            return;

        SolynDialogSystem system = ModContent.GetInstance<SolynDialogSystem>();
        system.DialogUI.SetDialogue(null);
        system.DialogUI.DialogueText = string.Empty;
        system.DialogUI.ResponseToSay = null;
        Visible = false;
    }
}
