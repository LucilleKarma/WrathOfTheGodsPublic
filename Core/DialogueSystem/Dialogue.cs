using Microsoft.Xna.Framework;

using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;

using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace NoxusBoss.Core.DialogueSystem;

public class Dialogue
{
    /// <summary>
    /// Whether this dialogue is spoken by the player, rather than Solyn.
    /// </summary>
    public bool SpokenByPlayer
    {
        get;
        set;
    }

    /// <summary>
    /// The localization key that points to this dialogue.
    /// </summary>
    public string TextKey
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this dialogue has been seen before.
    /// </summary>
    public bool SeenBefore => DialogueSaveSystem.seenDialogue.Contains(TextKey);

    /// <summary>
    /// The condition which dictates whether this dialogue can be chosen from a parent dialogue node in a dialogue tree.
    /// </summary>
    public Func<bool> SelectionCondition
    {
        get;
        set;
    } = () => true;

    /// <summary>
    /// An optional action that should be performed when traversing to a child node, effectively concluding the viewing of this dialogue node.
    /// </summary>
    public ClickAwayActionDelegate EndAction
    {
        internal get;
        set;
    } = _ => { };

    /// <summary>
    /// An optional action that should be performed when traversing to a child node via click, effectively concluding the viewing of this dialogue node.
    /// </summary>
    public ClickAwayActionDelegate ClickAction
    {
        internal get;
        set;
    } = _ => { };

    /// <summary>
    /// The child nodes for this dialogue.
    /// </summary>
    public List<Dialogue> Children
    {
        get;
        set;
    } = [];

    /// <summary>
    /// A function that may be used to override the text color for this node.
    /// </summary>
    public Func<Color>? ColorOverrideFunction
    {
        get;
        set;
    }

    /// <summary>
    /// The text associated with this dialogue.
    /// </summary>
    public string Text => Language.GetTextValue(TextKey);

    public delegate void ClickAwayActionDelegate(bool hasBeenSeenBefore);

    public Dialogue(string textKey)
    {
        TextKey = textKey;
    }

    /// <summary>
    /// Invokes the <see cref="EndAction"/>.
    /// </summary>
    public void InvokeEndAction(bool serverAction = false)
    {
        if (!serverAction && Main.netMode == NetmodeID.MultiplayerClient)
        {
            PacketManager.SendPacket<DialogueEventPacket>(TextKey, DialogueEventPacket.END_ACTION);
        }

        bool seenBefore = true;
        if (!DialogueSaveSystem.seenDialogue.Contains(TextKey))
        {
            DialogueSaveSystem.seenDialogue.Add(TextKey);
            seenBefore = false;
        }

        EndAction(seenBefore);
    }

    /// <summary>
    /// Invokes the <see cref="ClickAction"/>.
    /// </summary>
    public void InvokeClickAction(bool serverAction = false)
    {
        if (!serverAction && Main.netMode == NetmodeID.MultiplayerClient)
        {
            PacketManager.SendPacket<DialogueEventPacket>(TextKey, DialogueEventPacket.CLICK_ACTION);
        }

        bool clickedBefore = true;
        if (!DialogueSaveSystem.clickedDialogue.Contains(TextKey))
        {
            DialogueSaveSystem.clickedDialogue.Add(TextKey);
            clickedBefore = false;
        }
        if (!DialogueSaveSystem.seenDialogue.Contains(TextKey))
            DialogueSaveSystem.seenDialogue.Add(TextKey);

        ClickAction(clickedBefore);
    }
}
