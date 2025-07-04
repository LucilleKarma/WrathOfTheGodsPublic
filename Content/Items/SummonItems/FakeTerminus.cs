using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.Projectiles;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.World.GameScenes.TerminusStairway;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.SummonItems;

public class FakeTerminus : ModItem
{
    private static int realTerminusID;

    public static bool Exists
    {
        get;
        private set;
    }

    public static int TerminusID
    {
        get
        {
            if (Exists)
                return ModContent.ItemType<FakeTerminus>();

            return realTerminusID;
        }
    }

    /// <summary>
    /// Whether the Terminus can be used or not.
    /// </summary>
    public static bool CanUse => BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() || Main.zenithWorld;

    public override string Texture => GetAssetPath("Content/Items/SummonItems", "Terminus");

    public override bool IsLoadingEnabled(Mod mod)
    {
        // Determine if the fake Terminus should exist.
        // It does not exist if Calamity is loaded and contains the Terminus, but serves as a backup otherwise.
        Exists = !ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal);
        if (cal?.TryFind("Terminus", out ModItem terminus) ?? false)
            realTerminusID = terminus.Type;
        else
            Exists = true;

        // Regardless of whether this item exists, however, apply Terminus functionality alterations.
        // This is done in a GlobalItem class, and will affect either this fake Terminus or the real one under the same terms, whichever exists.
        // The reason this loading is done in this hook is because it's not guaranteed that this fake item will have a chance to load, and if it doesn't then
        // the traditional loading hooks (such as SetStaticDefaults) are unreliable.
        GlobalItemEventHandlers.SetDefaultsEvent += ChangeTerminusProjectileSpawnType;
        GlobalItemEventHandlers.ModifyTooltipsEvent += ChangeTerminusTooltipDialog;
        GlobalItemEventHandlers.PreDrawInInventoryEvent += UseUnopenedEyeForm_Inventory;
        GlobalItemEventHandlers.PreDrawInWorldEvent += UseUnopenedEyeForm_World;
        GlobalItemEventHandlers.CanUseItemEvent += ModifyTerminusUseConditions;
        return Exists;
    }

    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

    private void ChangeTerminusProjectileSpawnType(Item item)
    {
        // Replace Terminus' projectile with a custom one that has nothing to do with Boss Rush.
        if (item.type == TerminusID)
        {
            item.shoot = ModContent.ProjectileType<TerminusProj>();
            item.channel = false;
        }
    }

    private void ChangeTerminusTooltipDialog(Item item, List<TooltipLine> tooltips)
    {
        // Alter the Terminus' tooltip text.
        if (item.type == TerminusID && !Main.zenithWorld)
        {
            string text = Language.GetTextValue("Mods.NoxusBoss.Items.FakeTerminus.OpenedTooltip");
            Color color = new Color(240, 76, 76);
            if (!CanUse)
            {
                text = Language.GetTextValue("Mods.NoxusBoss.Items.FakeTerminus.UnopenedTooltip");
                color = new Color(239, 174, 174);
            }

            if (tooltips.Count < 3)
            {
                tooltips.Add(new TooltipLine(ModContent.GetInstance<NoxusBoss>(), "Terraria:Tooltip1", text));
                tooltips.Last().OverrideColor = color;
            }

            EditTooltipByNum(0, item, tooltips, line => line.Text = Language.GetTextValue("Mods.NoxusBoss.Items.FakeTerminus.BaseTooltip"));
            EditTooltipByNum(1, item, tooltips, line =>
            {
                if (CanUse)
                {
                    line.OverrideColor = new Color(240, 76, 76);
                    line.Text = Language.GetTextValue("Mods.NoxusBoss.Items.FakeTerminus.OpenedTooltip");
                }
                else
                {
                    line.OverrideColor = new Color(239, 174, 174);
                    line.Text = Language.GetTextValue("Mods.NoxusBoss.Items.FakeTerminus.UnopenedTooltip");
                }
            });
        }
    }

    private bool UseUnopenedEyeForm_Inventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        // Let the Terminus use its closed eye form if the Avatar is not yet defeated.
        if (item.type == TerminusID && !CanUse && !Main.zenithWorld)
        {
            spriteBatch.Draw(GennedAssets.Textures.SummonItems.TerminusClosedEye.Value, position, null, Color.White, 0f, origin, scale, 0, 0);
            return false;
        }
        return true;
    }

    private bool UseUnopenedEyeForm_World(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        // Let the Terminus use its closed eye form if the Avatar is not yet defeated.
        if (item.type == TerminusID && !CanUse && !Main.zenithWorld)
        {
            spriteBatch.Draw(GennedAssets.Textures.SummonItems.TerminusClosedEye.Value, item.position - Main.screenPosition, null, lightColor, 0f, Vector2.Zero, 1f, 0, 0);
            return false;
        }
        return true;
    }

    private bool ModifyTerminusUseConditions(Item item, Player player)
    {
        // Make the Terminus only usable after the Avatar has been defeated. Also disallow it being usable to create multiple instances of Terminus in the world. That'd be weird.
        if (item.type == TerminusID)
        {
            if (AnyBosses())
                return false;

            return CanUse && player.ownedProjectileCounts[ModContent.ProjectileType<TerminusProj>()] <= 0 && !TerminusStairwaySystem.Enabled;
        }

        return true;
    }

    public override void SetDefaults()
    {
        Item.width = 60;
        Item.height = 70;
        Item.useAnimation = 40;
        Item.useTime = 40;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.UseSound = null;
        Item.value = 0;
        Item.rare = ItemRarityID.Blue;
    }
}
