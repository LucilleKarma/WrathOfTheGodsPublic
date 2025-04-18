using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Content.Tiles.SolynRopes;

public class SolynRopeSystem : ModSystem
{
    private static readonly List<SolynRopeData> ropes = new List<SolynRopeData>(8);

    /// <summary>
    /// Registers a new rope into the set of ropes maintained by the world.
    /// </summary>
    public static void Register(SolynRopeData rope)
    {
        bool ropeAlreadyExists = ropes.Any(r => (r.Start == rope.Start && r.End == rope.End) ||
                                                (r.Start == rope.End && r.End == rope.Start));
        if (ropeAlreadyExists)
            return;

        ropes.Add(rope);
    }

    /// <summary>
    /// Removes a given existing rope from the set of ropes maintained by the world.
    /// </summary>
    public static void Remove(SolynRopeData rope) => ropes.Remove(rope);

    public override void SaveWorldData(TagCompound tag)
    {
        tag["RopeCount"] = ropes.Count;

        TagCompound ropesTag = new TagCompound();
        for (int i = 0; i < ropes.Count; i++)
            ropesTag.Add($"Rope{i}", ropes[i].Serialize());

        tag["Ropes"] = ropesTag;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        ropes.Clear();

        if (!tag.TryGet("RopeCount", out int ropeCount) || !tag.TryGet("Ropes", out TagCompound ropeTag))
            return;

        for (int i = 0; i < ropeCount; i++)
            ropes.Add(SolynRopeData.Deserialize(ropeTag.GetCompound($"Rope{i}")));
    }

    public override void PostUpdatePlayers()
    {
        for (int i = 0; i < ropes.Count; i++)
        {
            SolynRopeData rope = ropes[i];
            rope.Update_Standard();

            // Account for the case in which a rope gets removed in the middle of the loop.
            if (!ropes.Contains(rope))
                i--;
        }
    }

    public override void PostDrawTiles()
    {
        if (ropes.Count <= 0)
            return;

        Main.spriteBatch.ResetToDefault(false);
        foreach (SolynRopeData rope in ropes)
            rope.Render(true, Color.White);

        Main.spriteBatch.End();
    }
}
