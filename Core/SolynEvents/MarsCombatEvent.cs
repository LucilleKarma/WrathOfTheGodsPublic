using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.World.Subworlds;
using SubworldLibrary;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.SolynEvents;

public class MarsCombatEvent : SolynEvent
{
    /// <summary>
    /// Whether Mars was summoned by the codebreaker.
    /// </summary>
    public static bool MarsBeingSummoned
    {
        get;
        set;
    }

    /// <summary>
    /// Whether Dreadon has been spoken to before or not.
    /// </summary>
    public static bool HasSpokenToDraedonBefore
    {
        get;
        set;
    }

    public override int TotalStages => 2;

    public static bool CanStart => ModContent.GetInstance<AntiseedEvent>().Finished && CommonCalamityVariables.DraedonDefeated;

    public override void OnModLoad()
    {
        Conversation conversation = DialogueManager.RegisterNew("DraedonBeforeCombatSimulation", "Start").
            LinkFromStartToFinish().
            WithAppearanceCondition(instance => CanStart).
            WithRerollCondition(instance => !instance.AppearanceCondition()).
            MakeSpokenByPlayer("Question1", "Question2", "Question3").
            WithRerollCondition(_ => Finished);

        conversation.GetByRelativeKey("Conversation5").EndAction = _ =>
        {
            SafeSetStage(1);
        };

        ConversationSelector.PriorityConversationSelectionEvent += SelectMarsDialogue;
    }

    private Conversation? SelectMarsDialogue()
    {
        if (SubworldSystem.IsActive<EternalGardenNew>())
            return null;

        if (!Finished && CanStart)
            return DialogueManager.FindByRelativePrefix("DraedonBeforeCombatSimulation");

        return null;
    }

    public override void PostUpdateNPCs()
    {
        if (Stage >= 1 && !Finished && Solyn is not null)
        {
            if (Solyn.CurrentState != SolynAIType.PuppeteeredByQuest)
                Solyn.SwitchState(SolynAIType.PuppeteeredByQuest);

            NPC npc = Solyn.NPC;
            Player player = Main.player[Player.FindClosest(npc.Center, 1, 1)];
            Vector2 flyDestination = player.Center + new Vector2(player.direction * -74f, -60f);
            Vector2 idealVelocity = (flyDestination - npc.Center).ClampLength(0f, player.velocity.Length() + 5f);

            npc.Center = Vector2.Lerp(npc.Center, flyDestination, 0.0125f);
            npc.velocity = Vector2.Lerp(npc.velocity, idealVelocity, 0.05f).MoveTowards(idealVelocity, 0.2f);
            if (Vector2.Dot(npc.velocity, idealVelocity) < 0f)
                npc.velocity *= 0.74f;

            npc.noGravity = true;
            npc.spriteDirection = (player.Center.X - npc.Center.X).NonZeroSign();

            Solyn.CanBeSpokenTo = false;
            Solyn.UseStarFlyEffects();
        }
    }

    public override void OnWorldLoad()
    {
        base.OnWorldLoad();
        MarsBeingSummoned = false;
        HasSpokenToDraedonBefore = false;
    }

    public override void OnWorldUnload()
    {
        base.OnWorldUnload();
        MarsBeingSummoned = false;
        HasSpokenToDraedonBefore = false;
    }

    public override void NetSend(BinaryWriter writer)
    {
        base.NetSend(writer);
        writer.Write(MarsBeingSummoned);
        writer.Write(HasSpokenToDraedonBefore);
    }

    public override void NetReceive(BinaryReader reader)
    {
        base.NetReceive(reader);
        MarsBeingSummoned = reader.ReadBoolean();
        HasSpokenToDraedonBefore = reader.ReadBoolean();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        base.SaveWorldData(tag);
        if (HasSpokenToDraedonBefore)
            tag["HasSpokenToDraedonBefore"] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        base.LoadWorldData(tag);
        HasSpokenToDraedonBefore = tag.ContainsKey("HasSpokenToDraedonBefore");
    }
}
