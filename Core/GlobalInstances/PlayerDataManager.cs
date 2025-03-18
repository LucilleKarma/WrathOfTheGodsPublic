using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.GlobalInstances;

[LegacyName("NoxusPlayer")]
public class PlayerDataManager : ModPlayer
{
    internal readonly ReferencedValueRegistry valueRegistry = new ReferencedValueRegistry();

    public Referenced<float> MaxFallSpeedBoost => valueRegistry.GetValueRef<float>("MaxFallSpeedBoost");

    public delegate void PlayerActionDelegate(PlayerDataManager p);

    public delegate bool PlayerConditionDelegate(PlayerDataManager p);

    public static event PlayerActionDelegate? ResetEffectsEvent;

    public delegate void SaveLoadDataDelegate(PlayerDataManager p, TagCompound tag);

    public delegate void ColorChangeDelegate(PlayerDataManager p, ref Color drawColor);

    public delegate void CatchFishDelegate(PlayerDataManager p, FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition);

    public delegate void MaxStatsDelegate(PlayerDataManager p, ref StatModifier health, ref StatModifier mana);

    public static event SaveLoadDataDelegate? SaveDataEvent;

    public static event SaveLoadDataDelegate? LoadDataEvent;

    public static event PlayerActionDelegate? PostMiscUpdatesEvent;

    public static event PlayerActionDelegate? PostUpdateEvent;

    public static event ColorChangeDelegate? GetAlphaEvent;

    public static event MaxStatsDelegate? MaxStatsEvent;

    public static event CatchFishDelegate? CatchFishEvent;

    public static event PlayerActionDelegate? UpdateBadLifeRegenEvent;

    public delegate void PlayerHideLayersDelegate(PlayerDataManager p, ref PlayerDrawSet drawInfo);

    public static event PlayerHideLayersDelegate? PlayerHideLayersEvent;

    public static event PlayerHideLayersDelegate? PlayerModifyDrawInfoEvent;

    public delegate bool PreKillDelegate(PlayerDataManager p, ref PlayerDeathReason damageSource);

    public static event PreKillDelegate? PreKillEvent;

    public static event PlayerConditionDelegate? ImmuneToEvent;

    public delegate bool CanUseItemDelegate(PlayerDataManager p, Item item);

    public static event CanUseItemDelegate? CanUseItemEvent;

    public delegate void AnglerQuestRewardDelegate(PlayerDataManager p, float rareMultiplier, List<Item> rewardItems);

    public static event AnglerQuestRewardDelegate? AnglerQuestRewardEvent;

    public delegate void OnHurtDelegate(PlayerDataManager p, Player.HurtInfo hurtInfo);

    public static event OnHurtDelegate? OnHurtEvent;

    public delegate void ModifyHurtDelegate(PlayerDataManager p, ref Player.HurtModifiers hurtInfo);

    public static event ModifyHurtDelegate? ModifyHurtEvent;

    public override void Load()
    {
        new ManagedILEdit("Update Max Fall Speed", Mod, edit =>
        {
            IL_Player.Update += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Player.Update -= edit.SubscriptionWrapper;
        }, UpdateMaxFallSpeed).Apply();
    }

    public override void Unload()
    {
        // Reset all events on mod unload.
        ResetEffectsEvent = null;
        SaveDataEvent = null;
        LoadDataEvent = null;
        PostUpdateEvent = null;
        GetAlphaEvent = null;
        MaxStatsEvent = null;
    }

    private static void UpdateMaxFallSpeed(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchLdfld<Player>("vortexDebuff")))
        {
            edit.LogFailure("The vortexDebuff load could not be found.");
            return;
        }

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Action<Player>>(player =>
        {
            player.maxFallSpeed += player.GetModPlayer<PlayerDataManager>().MaxFallSpeedBoost;
        });
    }

    public override void SetStaticDefaults()
    {
        On_Player.GetImmuneAlpha += GetAlphaOverride;
        On_Player.GetImmuneAlphaPure += GetAlphaOverride2;
    }

    public override void UpdateBadLifeRegen()
    {
        UpdateBadLifeRegenEvent?.Invoke(this);
    }

    private static Color ApplyGetAlpha(Player player, Color result)
    {
        // Don't do anything if the event has no subcribers.
        if (GetAlphaEvent is null)
            return result;

        // Get the mod player instance.
        PlayerDataManager p = player.GetModPlayer<PlayerDataManager>();

        // Apply subscriber contents to the resulting color.
        foreach (Delegate d in GetAlphaEvent.GetInvocationList())
            ((ColorChangeDelegate)d).Invoke(p, ref result);

        return result;
    }

    private static Color GetAlphaOverride(On_Player.orig_GetImmuneAlpha orig, Player self, Color newColor, float alphaReduction)
    {
        return ApplyGetAlpha(self, orig(self, newColor, alphaReduction));
    }

    private static Color GetAlphaOverride2(On_Player.orig_GetImmuneAlphaPure orig, Player self, Color newColor, float alphaReduction)
    {
        return ApplyGetAlpha(self, orig(self, newColor, alphaReduction));
    }

    public override void ResetEffects()
    {
        // Apply the reset effects event.
        ResetEffectsEvent?.Invoke(this);
    }

    public override void SaveData(TagCompound tag)
    {
        // Apply the save data event.
        SaveDataEvent?.Invoke(this, tag);
    }

    public override void LoadData(TagCompound tag)
    {
        // Apply the load data event.
        LoadDataEvent?.Invoke(this, tag);
    }

    public override void PostUpdate()
    {
        // Apply the post-update event.
        PostUpdateEvent?.Invoke(this);
    }

    public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        // Do nothing by default to stats.
        health = StatModifier.Default;
        mana = StatModifier.Default;

        // Apply the stat modification event.
        MaxStatsEvent?.Invoke(this, ref health, ref mana);
    }

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        PlayerModifyDrawInfoEvent?.Invoke(this, ref drawInfo);
    }

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        PlayerHideLayersEvent?.Invoke(this, ref drawInfo);
    }

    public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        // Apply the fish catching event.
        CatchFishEvent?.Invoke(this, attempt, ref itemDrop, ref npcSpawn, ref sonar, ref sonarPosition);
    }

    public override void AnglerQuestReward(float rareMultiplier, List<Item> rewardItems) =>
        AnglerQuestRewardEvent?.Invoke(this, rareMultiplier, rewardItems);

    public override void PostUpdateMiscEffects()
    {
        MaxFallSpeedBoost.Value = 0f;
        PostMiscUpdatesEvent?.Invoke(this);
    }

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
    {
        if (ImmuneToEvent is null)
            return false;

        foreach (Delegate d in ImmuneToEvent.GetInvocationList())
        {
            if (((PlayerConditionDelegate)d).Invoke(this))
                return true;
        }
        return false;
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        OnHurtEvent?.Invoke(this, info);
    }

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        ModifyHurtEvent?.Invoke(this, ref modifiers);
    }

    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
    {
        if (PreKillEvent is null)
            return true;

        foreach (Delegate d in PreKillEvent.GetInvocationList())
        {
            if (!((PreKillDelegate)d).Invoke(this, ref damageSource))
                return false;
        }
        return true;
    }

    public override bool CanUseItem(Item item)
    {
        if (CanUseItemEvent is null)
            return true;

        foreach (Delegate d in CanUseItemEvent.GetInvocationList())
        {
            if (!((CanUseItemDelegate)d).Invoke(this, item))
                return false;
        }
        return true;
    }
}
