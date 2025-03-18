using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.World.Subworlds;
using SubworldLibrary;
using Terraria;

namespace NoxusBoss.Core.DialogueSystem;

public static class ConversationSelector
{
    /// <summary>
    /// An event used to dictate whether certain conversations have priority over others.
    /// </summary>
    public static event Func<Conversation?> PriorityConversationSelectionEvent;

    /// <summary>
    /// The set of all conversations that Solyn can potentially chose from if nothing of greater importance is going on.
    /// </summary>
    public static readonly List<FallbackConversation> FallbackConversations = new List<FallbackConversation>(16);

    /// <summary>
    /// Represents a fallback conversation that can be used when nothing of importance is going on.
    /// </summary>
    /// <param name="Conversation">The conversation data.</param>
    /// <param name="Priority">The priority of the fallback conversation. Used to make certain conversations appear based on more 'important' events, such as slime rain.</param>
    public record FallbackConversation(Conversation Conversation, int Priority);

    public static Conversation ChooseRandomSolynConversation(Solyn solyn)
    {
        List<FallbackConversation> fallbackConversations = FallbackConversations.Where(c => c.Conversation.AppearanceCondition() && !c.Conversation.RerollCondition()).ToList();
        if (fallbackConversations.Count >= 1)
        {
            int maxPriority = fallbackConversations.Max(c => c.Priority);
            if (fallbackConversations.Count >= 1)
                return Main.rand.Next(fallbackConversations.Where(c => c.Priority == maxPriority).Select(c => c.Conversation).ToArray());
        }

        return DialogueManager.FindByRelativePrefix("SolynErrorFallback");
    }

    private static void CheckForPriorityConversations(Solyn solyn)
    {
        // Special case: Solyn needs to select the garden dialogue in the garden subworld.
        if (SubworldSystem.IsActive<EternalGardenNew>())
        {
            solyn.CurrentConversation = DialogueManager.FindByRelativePrefix("PostNamelessDiscussion");
            return;
        }

        // Check for if there's any high priority conversations that should be used.
        Conversation? priorityConversation = null;
        foreach (Delegate d in PriorityConversationSelectionEvent.GetInvocationList())
        {
            Func<Conversation?> selectorFunction = (Func<Conversation?>)d;
            Conversation? result = selectorFunction();

            if (result is not null)
            {
                priorityConversation = result;
                break;
            }
        }

        if (priorityConversation is not null && solyn.CurrentConversation != priorityConversation)
            solyn.CurrentConversation = priorityConversation;
        else
            CheckForHigherPriorityFallbackConversation(solyn);
    }

    private static void CheckForHigherPriorityFallbackConversation(Solyn solyn)
    {
        // Don't do anything if Solyn's conversation isn't fallback dialogue.
        FallbackConversation? currentFallbackConversation = FallbackConversations.FirstOrDefault(c => c.Conversation == solyn.CurrentConversation);
        if (currentFallbackConversation is null)
            return;

        // Check if any of the potential conversations have a higher priority than the current one.
        // If one does, select it instead.
        List<FallbackConversation> potentialConversations = FallbackConversations.Where(c => c.Conversation.AppearanceCondition() && !c.Conversation.RerollCondition()).ToList();
        foreach (FallbackConversation potentialConversation in potentialConversations)
        {
            if (potentialConversation.Priority > currentFallbackConversation.Priority)
            {
                solyn.CurrentConversation = potentialConversation.Conversation;
                break;
            }
        }
    }

    private static void CheckForRerolls(Solyn solyn)
    {
        bool beingSpokenTo = (Main.LocalPlayer.talkNPC == solyn.NPC.whoAmI && solyn.CanBeSpokenTo) || solyn.ForcedConversation;
        if (!beingSpokenTo && solyn.CurrentConversation.RerollCondition())
            solyn.CurrentConversation = ChooseRandomSolynConversation(solyn);
    }

    /// <summary>
    /// Evalutes Solyn's current dialogue, switching it to something else if necessary.
    /// </summary>
    public static void Evaluate(Solyn solyn)
    {
        CheckForPriorityConversations(solyn);
        CheckForRerolls(solyn);
    }

    /// <summary>
    /// Registers a given conversation as something Solyn can say as a fallback.
    /// </summary>
    public static Conversation MakeFallback(this Conversation conversation, int priority = 0)
    {
        FallbackConversations.Add(new FallbackConversation(conversation, priority));
        return conversation;
    }
}
