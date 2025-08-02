using System.Text;
using Hjson;
using Newtonsoft.Json.Linq;
using Terraria.ModLoader;

namespace NoxusBoss.Core.DialogueSystem;

public class DialogueTree
{
    /// <summary>
    /// Whether anything in this dialogue tree has been seen before.
    /// </summary>
    public bool HasBeenSeenBefore => PossibleDialogue.Values.Any(d => d.SeenBefore);

    /// <summary>
    /// The root dialogue node for this tree.
    /// </summary>
    public readonly Dialogue Root;

    /// <summary>
    /// The set of all dialogue that exists within this dialogue tree.
    /// </summary>
    public readonly Dictionary<string, Dialogue> PossibleDialogue = [];

    public DialogueTree(string localizationPrefix, string rootNodeKey)
    {
        List<(string, string)> flattened = new List<(string, string)>();
        string translationFileContents = Encoding.UTF8.GetString(ModContent.GetInstance<NoxusBoss>().GetFileBytes("Localization/en-US/Mods.NoxusBoss.Solyn.hjson"));
        string jsonString = HjsonValue.Parse(translationFileContents).ToString();
        JObject? jsonObject = JObject.Parse(jsonString);
        foreach (JToken t in jsonObject.SelectTokens("$..*"))
        {
            if (t.HasValues)
                continue;

            // Due to comments, some objects can by empty.
            if (t is JObject obj && obj.Count == 0)
                continue;

            // Custom implementation of Path to allow "x.y" keys.
            string path = "";
            JToken current = t;

            for (JToken parent = t.Parent!; parent != null; parent = parent.Parent!)
            {
                path = parent switch
                {
                    JProperty property => property.Name + (path == string.Empty ? string.Empty : "." + path),
                    JArray array => array.IndexOf(current) + (path == string.Empty ? string.Empty : "." + path),
                    _ => path
                };
                current = parent;
            }

            // removing instances of .$parentVal is an easy way to make this special key assign its value
            //  to the parent key instead (needed for some cases of .lang -> .hjson auto-conversion)
            path = path.Replace(".$parentVal", string.Empty);
            path = $"Mods.NoxusBoss.Solyn.{path}.";

            if (path.Contains(localizationPrefix))
                flattened.Add((path, t.ToString()));
        }

        foreach ((string, string) kv in flattened)
        {
            string key = kv.Item1.TrimEnd('.');
            Dialogue dialogue = new(key);
            PossibleDialogue[key.Replace(localizationPrefix, string.Empty)] = dialogue;
        }

        Root = PossibleDialogue[rootNodeKey];
    }

    /// <summary>
    /// Gets a given dialogue instance based on a relative key.
    /// </summary>
    public Dialogue GetByRelativeKey(string key) => PossibleDialogue[key];

    /// <summary>
    /// Links chains of dialogue together.
    /// </summary>
    public void LinkChain(params string[] identifiers)
    {
        for (int i = 0; i < identifiers.Length - 1; i++)
        {
            Dialogue current = PossibleDialogue[identifiers[i]];
            Dialogue next = PossibleDialogue[identifiers[i + 1]];

            if (!current.Children.Contains(next))
                current.Children.Add(next);
        }
    }
}
