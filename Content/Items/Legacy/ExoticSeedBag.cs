using NoxusBoss.Content.Items.GenesisComponents;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.DataStructures.Conditions;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Legacy
{
    [LegacyName("Genesis")]
    public class ExoticSeedBag : ModItem
    {
        public override string Texture => GetAssetPath("Content/Items/Legacy", Name);

        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 34;
            Item.consumable = true;
            Item.rare = ModContent.RarityType<SolynRewardRarity>();
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            itemLoot.Add(new CommonDrop(ModContent.ItemType<FrostblessedSeedlingItem>(), 1));
            itemLoot.Add(new CommonDrop(ModContent.ItemType<TheAntiseed>(), 1));
            itemLoot.Add(new CommonDrop(ModContent.ItemType<SyntheticSeedling>(), 1));
            itemLoot.Add(new CommonDrop(ModContent.ItemType<BlazingBud>(), 1));
        }

        public override void AddRecipes()
        {
            ModTile? forge = null;
            ModItem? bar = null;
            bool calExists = ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal);
            bool draedonForgeExists = cal?.TryFind("DraedonsForge", out forge) ?? false;
            bool shadowspecBarExists = cal?.TryFind("ShadowspecBar", out bar) ?? false;

            // Calamity Recipe, uses shadowspec bars and the Draedon's Forge.
            if (calExists && draedonForgeExists && shadowspecBarExists)
            {
                CreateRecipe().
                    AddTile(forge!.Type).
                    AddIngredient(ItemID.StoneBlock, 50).
                    AddIngredient(bar!.Type, 10).
                    AddCondition(CustomConditions.PreAvatarUpdateWorld).
                    Register();
            }

            // Vanilla Recipe, swaps the shadowspec with luminite and the forge with the Ancient Manipulator.
            else
            {
                CreateRecipe().
                    AddTile(TileID.LunarCraftingStation).
                    AddIngredient(ItemID.StoneBlock, 50).
                    AddIngredient(ItemID.LunarBar, 10).
                    AddCondition(CustomConditions.PreAvatarUpdateWorld).
                    Register();
            }
        }
    }
}
