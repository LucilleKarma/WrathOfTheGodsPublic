using SubworldLibrary;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace NoxusBoss.Core.World.WorldSaving;

public class WorldVersionSystem : ModSystem
{
    /// <summary>
    /// The world's current version.
    /// </summary>
    public static string WorldVersionText
    {
        get;
        set;
    } = OldVersionText;

    /// <summary>
    /// Whether the current world was generated before the Avatar update.
    /// </summary>
    public static bool PreAvatarUpdateWorld => WorldVersionText == OldVersionText;

    /// <summary>
    /// The build version of the current world.
    /// </summary>
    public static Version WorldVersion => PreAvatarUpdateWorld ? new Version(1, 0, 0, 0) : new Version(WorldVersionText);

    /// <summary>
    /// The fallback version text used for worlds generated before the Avatar update.
    /// </summary>
    public static string OldVersionText => "Before Avatar Update";

    public override void OnWorldLoad()
    {
        if (!SubworldSystem.AnyActive())
            WorldVersionText = OldVersionText;
    }

    public override void OnWorldUnload()
    {
        if (!SubworldSystem.AnyActive())
            WorldVersionText = OldVersionText;
    }

    public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        tasks.Add(new PassLegacy("Saving World Creation Version", (GenerationProgress progress, GameConfiguration config) =>
        {
            WorldVersionText = ModContent.GetInstance<NoxusBoss>().Version.ToString();
        }));
    }

    public override void SaveWorldData(TagCompound tag) => tag["WorldVersionText"] = WorldVersionText;

    public override void LoadWorldData(TagCompound tag)
    {
        if (tag.TryGet("WorldVersionText", out string version))
            WorldVersionText = version;
        else
            WorldVersionText = OldVersionText;
    }

    public override void NetSend(BinaryWriter writer) => writer.Write(WorldVersionText);

    public override void NetReceive(BinaryReader reader) => WorldVersionText = reader.ReadString();
}
