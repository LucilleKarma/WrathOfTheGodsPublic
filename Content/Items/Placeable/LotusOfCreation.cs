using Luminance.Core.Graphics;
using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.Graphics.ItemPreRender;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Placeable;

public class LotusOfCreation : ModItem, IPreRenderedItem
{
    public class LotusOfCreationRarity : ModRarity
    {
        /// <summary>
        /// The palette that this rarity cycles through.
        /// </summary>
        public static readonly Palette RarityPalette = new Palette().
            AddColor(new Color(0, 0, 0)).
            AddColor(new Color(71, 35, 137)).
            AddColor(new Color(120, 60, 231)).
            AddColor(new Color(46, 156, 211));

        public override Color RarityColor => RarityPalette.SampleColor(Main.GlobalTimeWrappedHourly * 0.2f % 1f) * 1.4f;
    }

    public override string Texture => GetAssetPath("Content/Items/Placeable", Name);

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;
        On_Player.SellItem += DisallowSelling;
        new ManagedILEdit("Use Special Sell Text for Lotus of Creation", Mod, edit =>
        {
            IL_Main.MouseText_DrawItemTooltip += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Main.MouseText_DrawItemTooltip -= edit.SubscriptionWrapper;
        }, UseSpecialSellText).Apply(false);
    }

    private bool DisallowSelling(On_Player.orig_SellItem orig, Player self, Item item, int stack)
    {
        if (item.type == Type)
            return false;

        return orig(self, item, stack);
    }

    private void UseSpecialSellText(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        if (!cursor.TryGotoNext(MoveType.Before, c => c.MatchLdcI4(49)))
        {
            edit.LogFailure("The 49 integer constant load could not be found!");
            return;
        }

        if (!cursor.TryGotoNext(MoveType.Before, c => c.MatchStelemRef()))
        {
            edit.LogFailure("The element reference storage could not be found!");
            return;
        }

        cursor.EmitDelegate((string originalText) =>
        {
            if (Main.HoverItem.type == Type)
                return this.GetLocalizedValue("SellText");

            return originalText;
        });
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<LotusOfCreationTile>());
        Item.width = 66;
        Item.height = 20;
        Item.rare = ModContent.RarityType<LotusOfCreationRarity>();
        Item.value = Item.sellPrice(0, 35, 0, 0);
        Item.consumable = true;
    }

    public void PreRender(Texture2D sourceTexture)
    {
        Vector3[] palette = LotusOfCreationTile.ShaderPalette;
        ManagedShader lotusShader = ShaderManager.GetShader("NoxusBoss.LotusOfCreationShader");
        lotusShader.TrySetParameter("appearanceInterpolant", 1f);
        lotusShader.TrySetParameter("gradient", palette);
        lotusShader.TrySetParameter("gradientCount", palette.Length);
        lotusShader.Apply();

        Main.spriteBatch.Draw(sourceTexture, Vector2.Zero, Color.White);
    }
}

