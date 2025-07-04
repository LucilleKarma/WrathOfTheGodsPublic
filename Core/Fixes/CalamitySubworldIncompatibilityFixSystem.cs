using System.Reflection;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.World.Subworlds;
using SubworldLibrary;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Fixes;

/* CONTEXT:
 * Calamity executes its hardcoded index code for random tile growth (such as lumenyl on voidstone) and abyss checks for the sulphurous sea in a way that does not
 * respect subworlds. As such, when Calamity is on such behaviors should be disabled in subworlds.
 */
public class CalamitySubworldIncompatibilityFixSystem : ModSystem
{
    public delegate void orig_TileGrowthMethod();

    public delegate void hook_TileGrowthMethod(orig_TileGrowthMethod orig);

    public delegate bool orig_IsSulphSeaActiveMethod(object instance, Player player);

    public delegate bool hook_IsSulphSeaActiveMethod(orig_IsSulphSeaActiveMethod orig, object instance, Player player);

    public override void OnModLoad()
    {
        if (!ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal))
            return;

        MethodInfo? tileGrowMethod = cal.Code.GetType("CalamityMod.Systems.WorldMiscUpdateSystem")?.GetMethod("HandleTileGrowth", BindingFlags.Public | BindingFlags.Static) ?? null;
        if (tileGrowMethod is null)
        {
            Mod.Logger.Warn("Calamity's 'WorldMiscUpdateSystem' type could not be found! The sulphurous sea subworld interaction fix could not be applied!");
            return;
        }

        MethodInfo? sulphSeaActiveMethod = cal.Code.GetType("CalamityMod.BiomeManagers.SulphurousSeaBiome")?.GetMethod("IsBiomeActive", BindingFlags.Public | BindingFlags.Instance) ?? null;
        if (sulphSeaActiveMethod is null)
        {
            Mod.Logger.Warn("Calamity's 'SulphurousSeaBiome' type could not be found! The sulphurous sea subworld interaction fix could not be applied!");
            return;
        }

        MonoModHooks.Add(tileGrowMethod, (hook_TileGrowthMethod)DisableTileGrowthInGarden);
        MonoModHooks.Add(sulphSeaActiveMethod, (hook_IsSulphSeaActiveMethod)DisableSulphSeaInSubworlds);
    }

    public static void DisableTileGrowthInGarden(orig_TileGrowthMethod orig)
    {
        // This method throws a shitstorm of index errors when in the garden due to differing world sizes.
        // There's never going to be any natural sunken sea or abyss tiles to grow things on, so it's easiest to just tell that method to fuck off while in there.
        if (!SubworldSystem.IsActive<EternalGardenNew>())
            orig();
    }

    public static bool DisableSulphSeaInSubworlds(orig_IsSulphSeaActiveMethod orig, object instance, Player player)
    {
        // Hardcoded proximity checks don't work in subworlds.
        if (SubworldSystem.IsActive<EternalGardenNew>())
            return false;

        return orig(instance, player);
    }
}
