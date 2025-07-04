using CalamityMod;
using CalamityMod.CalPlayer;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;

public class CalamityCompatibility : ModSystem
{
    /// <summary>
    /// The internal name of the Calamity mod.
    /// </summary>
    public const string ModName = "CalamityMod";

    /// <summary>
    /// The Calamity mod's <see cref="Mod"/> instance.
    /// </summary>
    public static Mod Calamity
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether the Calamity Mod is enabled.
    /// </summary>
    public static bool Enabled => ModLoader.HasMod(ModName);

    public override void OnModLoad()
    {
        if (ModLoader.TryGetMod(ModName, out Mod cal))
            Calamity = cal;
    }

    /// <summary>
    /// Disables Calamity's special boss bar for a given <see cref="NPC"/>, such that it closes. This effect is temporarily and must be used every frame to sustain the close.
    /// </summary>
    /// <param name="npc">The NPC to change.</param>
    public static void MakeCalamityBossBarClose(NPC npc)
    {
        if (ModReferences.Calamity is null || Main.gameMenu)
            return;

        ModReferences.Calamity.Call("SetShouldCloseBossHealthBar", npc, true);
    }

    /// <summary>
    /// Makes an <see cref="NPC"/> immune to miracle blight, since it's notorious for creating odd visuals over the bosses.
    /// </summary>
    /// <param name="npc">The NPC to apply the immunity to.</param>
    public static void MakeImmuneToMiracleblight(NPC npc)
    {
        // Don't do anything if Calamity is not enabled.
        if (ModReferences.Calamity is null)
            return;

        // If miracle-blight could not be found, stop here and make a note in the logs that it couldn't be found.
        if (!ModReferences.Calamity.TryFind("MiracleBlight", out ModBuff miracleBlight))
            return;

        // Apply the immunity.
        NPCID.Sets.SpecificDebuffImmunity[npc.type][miracleBlight.Type] = true;
    }

    /// <summary>
    /// Applies extra HP to a given NPC in accordance with Calamity's boss health boost config.
    /// </summary>
    /// <param name="npc">The NPC to apply the health boost to.</param>
    [JITWhenModsEnabled(ModName)]
    public static void SetLifeMaxByMode_ApplyCalBossHPBoost(NPC npc)
    {
        long effectiveNewHP = npc.lifeMax + (long)Math.Round(npc.lifeMax * (double)(CalamityConfig.Instance.BossHealthBoost * 0.01));
        effectiveNewHP = Utils.Clamp(effectiveNewHP, 1, int.MaxValue);
        npc.lifeMax = (int)effectiveNewHP;
    }

    /// <summary>
    /// Gives a given <see cref="Player"/> the Boss Effects buff from Calamity, if it's enabled. This buff provides a variety of common effects, such as the near complete removal of natural enemy spawns.
    /// </summary>
    /// <param name="p">The player to apply the buff to.</param>
    public static void GrantBossEffectsBuff(Player p)
    {
        if (ModReferences.Calamity is null)
            return;

        if (!ModReferences.Calamity.TryFind("BossEffects", out ModBuff bossEffects))
            return;

        p.AddBuff(bossEffects.Type, 2);
    }

    /// <summary>
    /// Grants a given player infinite flight, taking into account Calamity's mod calls.
    /// </summary>
    /// <param name="player">The player to provide flight to.</param>
    public static void GrantInfiniteCalFlight(Player player)
    {
        player.GrantInfiniteFlight();
        if (ModLoader.TryGetMod(ModName, out Mod cal))
            cal.Call("EnableInfiniteFlight", player, true);
    }

    /// <summary>
    /// Resets rage and adrenaline for a given <see cref="Player"/>.
    /// </summary>
    /// <param name="p">The player to reset ripper values for.</param>
    public static void ResetRippers(Player p)
    {
        if (Enabled)
            ResetRippersWrapper(p);
    }

    [JITWhenModsEnabled(ModName)]
    private static void ResetRippersWrapper(Player p)
    {
        CalamityPlayer calPlayer = p.Calamity();
        calPlayer.rage = 0f;
        calPlayer.adrenaline = 0f;
    }

    /// <summary>
    /// Resets the opacity of a given player's stealth bar.
    /// </summary>
    /// <param name="p">The player to reset the stealth bar opacity of.</param>
    public static void ResetStealthBarOpacity(Player p)
    {
        if (Enabled)
            ResetStealthBarOpacityWrapper(p);
    }

    [JITWhenModsEnabled(ModName)]
    private static void ResetStealthBarOpacityWrapper(Player p)
    {
        CalamityPlayer calPlayer = p.Calamity();
        calPlayer.stealthUIAlpha = -0.25f;
    }
}
