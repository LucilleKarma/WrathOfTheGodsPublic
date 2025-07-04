using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.Items.SummonItems;
using NoxusBoss.Core.Autoloaders.SolynBooks;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.DataStructures.DropRules;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace NoxusBoss.Core.GlobalInstances;

[LegacyName("NoxusGlobalNPC")]
public partial class GlobalNPCEventHandlers : GlobalNPC
{
    private static int wyrmID = -1;

    internal ReferencedValueRegistry valueRegistry = new ReferencedValueRegistry();

    public delegate void EditSpawnRateDelegate(Player player, ref int spawnRate, ref int maxSpawns);

    public static event EditSpawnRateDelegate? EditSpawnRateEvent;

    public delegate void EditSpawnPoolDelegate(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo);

    public static event EditSpawnPoolDelegate? EditSpawnPoolEvent;

    public delegate void NPCActionDelegate(NPC npc);

    public static event NPCActionDelegate? OnKillEvent;

    public delegate bool NPCBoolDelegate(NPC npc);

    public static event NPCBoolDelegate? PreAIEvent;

    public static event NPCBoolDelegate? PreDrawEvent;

    public static event NPCBoolDelegate? CheckDeadEvent;

    public delegate void NPCSpawnDelegate(NPC npc, IEntitySource source);

    public static event NPCSpawnDelegate? OnSpawnEvent;

    public delegate void ModifyNPCLootDelegate(NPC npc, NPCLoot npcLoot);

    public static event ModifyNPCLootDelegate? ModifyNPCLootEvent;

    public delegate void ModifyGlobalLootDelegate(GlobalLoot globalLoot);

    public static event ModifyGlobalLootDelegate? ModifyGlobalLootEvent;

    public delegate void ModifyShopDelegate(NPCShop shop);

    public static event ModifyShopDelegate? ModifyShopEvent;

    public override bool InstancePerEntity => true;

    public override void Load()
    {
        // Load custom NPC IDs.
        if (ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal) && cal.TryFind("PrimordialWyrmHead", out ModNPC wyrm))
            wyrmID = wyrm.Type;
    }

    public override void Unload()
    {
        // Reset all events on mod unload.
        OnKillEvent = null;
        EditSpawnRateEvent = null;
        EditSpawnPoolEvent = null;
        OnSpawnEvent = null;
        PreAIEvent = null;
        PreDrawEvent = null;
    }

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        OnSpawnEvent?.Invoke(npc, source);
    }

    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        // The Primordial Wyrm drops the Terminus in Infernum instead of being in an abyss chest, since there is no abyss chest in Infernum.
        // For compatibility reasons, the Wyrm drops the boss rush starter item as well with this mod, along with the Terminus copy as necessary.
        // This item is only shown in the bestiary if Infernum is active, because in all other contexts it's unobtainable.
        if (npc.type == wyrmID)
        {
            LeadingConditionRule infernumActive = new LeadingConditionRule(new InfernumActiveDropRule());
            infernumActive.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Terminal>()));
            infernumActive.OnSuccess(ItemDropRule.Common(FakeTerminus.TerminusID));

            if (SolynBookAutoloader.Books.TryGetValue("AbsenceNotice", out AutoloadableSolynBook? book))
                infernumActive.OnSuccess(ItemDropRule.Common(book.Type));

            npcLoot.Add(infernumActive);
        }
        ModifyNPCLootEvent?.Invoke(npc, npcLoot);
    }

    public override void ModifyGlobalLoot(GlobalLoot globalLoot)
    {
        ModifyGlobalLootEvent?.Invoke(globalLoot);
    }

    public override bool PreAI(NPC npc)
    {
        // Use default behavior if the event has no subscribers.
        if (PreAIEvent is null)
            return true;

        // Apply AI alterations in accordance with the event.
        bool result = true;
        foreach (Delegate d in PreAIEvent.GetInvocationList())
            result &= ((NPCBoolDelegate)d).Invoke(npc);

        return result;
    }

    public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        // Use default behavior if the event has no subscribers.
        if (PreDrawEvent is null)
            return true;

        // Apply AI alterations in accordance with the event.
        bool result = true;
        foreach (Delegate d in PreDrawEvent.GetInvocationList())
            result &= ((NPCBoolDelegate)d).Invoke(npc);

        return result;
    }

    public override void OnKill(NPC npc)
    {
        OnKillEvent?.Invoke(npc);
    }

    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        // Apply spawn rate alterations in accordance with the event.
        EditSpawnRateEvent?.Invoke(player, ref spawnRate, ref maxSpawns);
    }

    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        // Apply spawn pool alterations in accordance with the event.
        EditSpawnPoolEvent?.Invoke(pool, spawnInfo);
    }

    public override bool CheckDead(NPC npc)
    {
        // Use default behavior if the event has no subscribers.
        if (CheckDeadEvent is null)
            return true;

        // Apply death effect modifications in accordance with the event.
        bool result = true;
        foreach (Delegate d in CheckDeadEvent.GetInvocationList())
            result &= ((NPCBoolDelegate)d).Invoke(npc);

        return result;
    }

    public static Referenced<T> GetValueRef<T>(NPC npc, string key) where T : struct =>
        npc.GetGlobalNPC<GlobalNPCEventHandlers>().valueRegistry.GetValueRef<T>(key);
}
