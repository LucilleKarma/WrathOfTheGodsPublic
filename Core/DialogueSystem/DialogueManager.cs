using Terraria.ModLoader;

namespace NoxusBoss.Core.DialogueSystem;

public class DialogueManager : ModSystem
{
    /// <summary>
    /// The set of all dialogue trees that exist in the mod.
    /// </summary>
    public static readonly Dictionary<string, Conversation> Conversations = new Dictionary<string, Conversation>(16);

    public override void OnModLoad()
    {
        // Error fallback.
        RegisterNew("SolynErrorFallback", "ErrorMessage1").
            LinkFromStartToFinish().
            WithRerollCondition(conversation => true); // Try to find something, ANYTHING else, if possible.
    }

    /// <summary>
    /// Finds a given conversation in the registry based on its relative prefix.
    /// </summary>
    public static Conversation FindByRelativePrefix(string prefix) =>
        Conversations[$"Mods.NoxusBoss.Solyn.{prefix}."];

    /// <summary>
    /// Registers a new dialogue tree that can be used by Solyn.
    /// </summary>
    /// <param name="localizationPrefix">The prefix for the dialogue tree in localization.</param>
    /// <param name="rootNodeKey">The relative key that identifies the root node.</param>
    public static Conversation RegisterNew(string localizationPrefix, string rootNodeKey)
    {
        // Sanity check so that I don't lose my mind over forgetting a period at the end of a string in the future.
        if (localizationPrefix[^1] != '.')
            localizationPrefix += '.';
        localizationPrefix = $"Mods.NoxusBoss.Solyn.{localizationPrefix}";

        Conversations[localizationPrefix] = new Conversation(localizationPrefix, rootNodeKey);
        return Conversations[localizationPrefix];
    }

    /// <summary>
    /// Finds a given dialogue by translation key
    /// </summary>
    public static Dialogue? FindDialogue(string textKey)
    {
        foreach (var conv in Conversations.Values)
        {
            foreach (var dialogue in conv.Tree.PossibleDialogue.Values)
            {
                if (dialogue.TextKey == textKey) return dialogue;
            }
        }

        return null;
    }
}
