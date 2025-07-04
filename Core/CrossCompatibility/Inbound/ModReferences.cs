using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using Terraria.ModLoader;

namespace NoxusBoss.Core.CrossCompatibility.Inbound;

public class ModReferences : ModSystem
{
    public static Mod Calamity
    {
        get;
        internal set;
    }

    public static Mod BossChecklistMod
    {
        get;
        private set;
    }

    // Goozma mod.
    public static Mod CalamityHuntMod
    {
        get;
        private set;
    }

    // Chaotic evil mod.
    public static Mod CalamityRemixMod
    {
        get;
        private set;
    }

    public static Mod InfernumMod
    {
        get;
        private set;
    }

    public static Mod OreExcavator
    {
        get;
        private set;
    }

    public static Mod NycrosNohitMod
    {
        get;
        private set;
    }

    public static Mod RealisticSky
    {
        get;
        private set;
    }

    public static Mod Wikithis
    {
        get;
        private set;
    }

    public static Mod ClickerClass
    {
        get;
        private set;
    }

    public override void Load()
    {
        // Check for relevant mods.
        if (ModLoader.TryGetMod("BossChecklist", out Mod bcl))
            BossChecklistMod = bcl;
        if (ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal))
            Calamity = cal;
        if (ModLoader.TryGetMod("CalamityHunt", out Mod calHunt))
            CalamityHuntMod = calHunt;
        if (ModLoader.TryGetMod("CalRemix", out Mod calRemix))
            CalamityRemixMod = calRemix;
        if (ModLoader.TryGetMod("InfernumMode", out Mod inf))
            InfernumMod = inf;
        if (ModLoader.TryGetMod("OreExcavator", out Mod veinminer))
            OreExcavator = veinminer;
        if (ModLoader.TryGetMod("EfficientNohits", out Mod nycros))
            NycrosNohitMod = nycros;
        if (ModLoader.TryGetMod("RealisticSky", out Mod sky))
            RealisticSky = sky;
        if (ModLoader.TryGetMod("Wikithis", out Mod wikithis))
            Wikithis = wikithis;
        if (ModLoader.TryGetMod("ClickerClass", out Mod clickerClass))
            ClickerClass = clickerClass;
    }
}
