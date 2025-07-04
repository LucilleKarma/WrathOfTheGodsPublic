using NoxusBoss.Core.GlobalInstances;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.WorldSaving;

public class GlobalBossDownedSaveSystem : ModSystem
{
    private static List<string> globalDefeatCache = new List<string>(4);

    /// <summary>
    /// The set of vanilla NPCs IDs to register as globally downed upon defeat in any world.
    /// </summary>
    public static bool[] VanillaNPCsToRegister
    {
        get;
        private set;
    }

    /// <summary>
    /// The path to the file that contains global defeats.
    /// </summary>
    public static string GlobalDefeatConfirmationFilePath => Path.Combine(Main.SavePath, "GlobalWotGDefeatList.txt");

    public override void OnModLoad()
    {
        VanillaNPCsToRegister = NPCID.Sets.Factory.CreateBoolSet(NPCID.MoonLordCore);

        // Ensure that the global defeat confirmation file exists.
        // If it doesn't, initialize it.
        // Otherwise, read its contents.
        if (!File.Exists(GlobalDefeatConfirmationFilePath))
            File.WriteAllText(GlobalDefeatConfirmationFilePath, string.Empty);
        else
            globalDefeatCache = File.ReadAllLines(GlobalDefeatConfirmationFilePath).ToList();

        GlobalNPCEventHandlers.OnKillEvent += RegisterVanillaNPCDeaths;
    }

    private void RegisterVanillaNPCDeaths(NPC npc)
    {
        if (npc.type < NPCID.Count && VanillaNPCsToRegister[npc.type])
            MarkDefeated(npc);
    }

    private static void MarkDefeated(string key)
    {
        if (globalDefeatCache.Contains(key))
            return;

        File.AppendAllLines(GlobalDefeatConfirmationFilePath, [key]);
        globalDefeatCache.Add(key);
    }

    /// <summary>
    /// Returns whether a given NPC ID is defeated.
    /// </summary>
    /// <param name="npcID">The type of NPC to evaluate.</param>
    public static bool IsDefeated(int npcID) => globalDefeatCache.Contains(npcID.ToString());

    /// <summary>
    /// Returns whether a given <see cref="ModNPC"/> of type <typeparamref name="TModNPC"/> is defeated.
    /// </summary>
    /// <typeparam name="TModNPC">The type of mod NPC to evaluate.</typeparam>
    public static bool IsDefeated<TModNPC>() where TModNPC : ModNPC
    {
        TModNPC? instance = ModContent.GetInstance<TModNPC>();

        // IsDefeated is used by mod menu IsAvailable properties.
        // However, those properties may be accessed before mod content is initialized.
        // To account for this, a null safety check is performed on the content singleton.
        if (instance is null)
            return false;

        return globalDefeatCache.Contains(instance.Name);
    }

    /// <summary>
    /// Marks a given NPC ID as defeated.
    /// </summary>
    /// 
    /// <remarks>
    /// This is intended to be used for vanilla NPC IDs. To mark <see cref="ModNPC"/>s as defeated, use <see cref="MarkDefeated{TModNPC}()"/> instead.
    /// </remarks>
    /// <param name="npcID">The type of NPC to mark as defeated.</param>
    public static void MarkDefeated(int npcID) => MarkDefeated(npcID.ToString());

    /// <summary>
    /// Marks a given <see cref="ModNPC"/> of type <typeparamref name="TModNPC"/> as defeated.
    /// </summary>
    /// <typeparam name="TModNPC">The type of mod NPC to mark as defeated.</typeparam>
    public static void MarkDefeated<TModNPC>() where TModNPC : ModNPC => MarkDefeated(ModContent.GetInstance<TModNPC>().Name);

    /// <summary>
    /// Marks a given <see cref="NPC"/> instance as defeated.
    /// </summary>
    public static void MarkDefeated(NPC npc)
    {
        if (npc.type < NPCID.Count)
            MarkDefeated(npc.type);
        else
            MarkDefeated(npc.ModNPC.Name);
    }
}
