﻿using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static NoxusBoss.Core.CrossCompatibility.Inbound.ModReferences;

namespace NoxusBoss.Core.CrossCompatibility.Inbound.CalamityRemix;

public class CalRemixCompatibilitySystem : ModSystem
{
    private static readonly Queue<FannyDialog> deferredDialogToRegister = new Queue<FannyDialog>();

    public class FannyDialog
    {
        private readonly object instance;

        public FannyDialog(string dialogKey, string portrait)
        {
            if (CalamityRemixMod is null)
                return;

            // Add the mod name in front of the identifier.
            string identifier = $"NoxusBoss_{dialogKey}";

            string dialog = Language.GetTextValue($"Mods.NoxusBoss.FannyDialog.{dialogKey}");
            instance = CalamityRemixMod.Call("CreateFannyDialog", identifier, dialog, portrait);
        }

        public FannyDialog WithoutPersistenceBetweenWorlds()
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("MakeFannyDialogNotPersist", instance);
            return this;
        }

        public FannyDialog WithoutClickability()
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("MakeFannyDialogNonClickable", instance);
            return this;
        }

        public FannyDialog WithCooldown(float cooldownInSeconds)
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("SetFannyDialogCooldown", instance, cooldownInSeconds);
            return this;
        }

        public FannyDialog WithCondition(Func<IEnumerable<NPC>, bool> condition)
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("AddFannyDialogCondition", instance, condition);
            return this;
        }

        public FannyDialog WithDrawSizes(int maxWidth = 380, float fontSizeFactor = 1f)
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("SetFannyDialogDrawSize", instance, maxWidth, fontSizeFactor);
            return this;
        }

        public FannyDialog WithDuration(float durationInSeconds)
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("SetFannyDialogDuration", instance, durationInSeconds);
            return this;
        }

        public FannyDialog WithRepeatability()
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("MakeFannyDialogRepeatable", instance);
            return this;
        }

        public FannyDialog WithEvilness()
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("MakeFannyDialogSpokenByEvilFanny", instance);
            return this;
        }

        public FannyDialog WithHoverItem(int itemID, float drawScale = 1f, Vector2 drawOffset = default)
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("AddFannyItemDisplay", instance, itemID, drawScale, drawOffset);
            return this;
        }

        public FannyDialog WithParentDialog(FannyDialog parent, float appearDelayInSeconds, bool parentNeedsToBeClickedOff = false)
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("ChainFannyDialog", parent.instance, instance, appearDelayInSeconds);
            if (!parentNeedsToBeClickedOff)
                return WithoutClickability().WithCondition(_ => true);

            return WithCondition(_ => true);
        }

        public FannyDialog WithHoverText(string hoverText)
        {
            if (CalamityRemixMod is null)
                return this;

            CalamityRemixMod.Call("SetFannyHoverText", instance, hoverText);
            return this;
        }

        public static bool JustReadLoreItem(int loreItemID)
        {
            (bool readLoreItem, int hoverItemID) = (Tuple<bool, int>)CalamityRemixMod.Call("GetFannyItemHoverInfo");

            return readLoreItem && hoverItemID == loreItemID;
        }

        public void Register()
        {
            if (CalamityRemixMod is null)
                return;

            if (Main.gameMenu)
            {
                deferredDialogToRegister.Enqueue(this);
                return;
            }

            CalamityRemixMod.Call("RegisterFannyDialog", instance);
        }
    }

    public static void MakeCountAsLoreItem(int loreItemID)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        CalamityRemixMod?.Call("MakeItemCountAsLoreItem", loreItemID);
    }

    public override void PreUpdateEntities()
    {
        while (deferredDialogToRegister.TryDequeue(out FannyDialog? dialog))
            dialog.Register();
    }
}
