using NoxusBoss.Core.DialogueSystem;

using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class DialogueEventPacket : Packet
{
    public const int CLICK_ACTION = 0;
    public const int END_ACTION = 1;

    public override void Read(BinaryReader reader)
    {
        var dialogueKey = reader.ReadString();
        var action = reader.ReadInt32();

        var dialogue = DialogueManager.FindDialogue(dialogueKey);
        if (dialogue is null) return;

        switch (action)
        {
            case CLICK_ACTION:
                dialogue.InvokeClickAction(true);
                break;
            case END_ACTION:
                dialogue.InvokeEndAction(true);
                break;
        }
    }

    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write((string)context[0]);
        packet.Write((int)context[1]);
    }
}
