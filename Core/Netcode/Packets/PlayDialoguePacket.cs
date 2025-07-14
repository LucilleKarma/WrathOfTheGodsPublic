using Microsoft.Xna.Framework;

using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.DialogueSystem;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Netcode.Packets;

public class PlayDialoguePacket : Packet
{
    public override void Write(ModPacket packet, params object[] context)
    {
        packet.Write((string)context[0]);
    }

    public override void Read(BinaryReader reader)
    {
        var dialogueKey = reader.ReadString();
        var dialogue = DialogueManager.FindDialogue(dialogueKey);
        if (dialogue is null) return;

        var solynIndex = NPC.FindFirstNPC(ModContent.NPCType<Solyn>());
        var solyn = Main.npc[solynIndex].As<Solyn>();
        if (solyn is null) return;

        if (dialogue.SpokenByPlayer)
        {
            if (solyn.TalkingTo == -1) return;

            var player = Main.player[solyn.TalkingTo];
            Main.NewText($"[{player.name}]: {dialogue.Text}");
        }
        else
        {
            Main.NewText($"[{solyn.DisplayName}]: {dialogue.Text}", Color.Yellow);
        }
    }
}
